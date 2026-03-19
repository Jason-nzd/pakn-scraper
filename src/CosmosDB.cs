using System.Data.Common;
using System.Diagnostics;
using Azure.Core.Diagnostics;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using static Scraper.Program;
using static Scraper.Utilities;

namespace Scraper
{
    public partial class CosmosDB
    {
        // CosmosDB singletons
        public static CosmosClient? cosmosClient;
        public static Database? database;
        public static Container? cosmosContainer;
        static string partitionKey = "/category";
        static string today = DateTime.Today.ToString("yyyy-MM-dd");

        // EstablishConnection()
        // ---------------------
        // Establishes a connection using settings defined in appsettings.json.
        public static async Task<bool> EstablishConnection()
        {
            // Read all config values upfront with proper null/empty validation
            string? endpoint = config["COSMOS_ENDPOINT"];
            string? key = config["COSMOS_KEY"];
            string? dbName = config["COSMOS_DB"];
            string? containerName = config["COSMOS_CONTAINER"];

            // Validate required configuration
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                LogError("COSMOS_ENDPOINT in appsettings.json is missing or empty");
                return false;
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                LogError("COSMOS_KEY in appsettings.json is missing or empty");
                return false;
            }
            if (string.IsNullOrWhiteSpace(dbName))
            {
                LogError("COSMOS_DB in appsettings.json is missing or empty");
                return false;
            }
            if (string.IsNullOrWhiteSpace(containerName))
            {
                LogError("COSMOS_CONTAINER in appsettings.json is missing or empty");
                return false;
            }

            try
            {
                // Create CosmosDB client
                cosmosClient = new CosmosClient(endpoint, key);
            }
            catch (Exception e)
            {
                LogError(e.GetType().ToString());
                LogError("Error Connecting to CosmosDB - check appsettings.json ");
                Environment.Exit(1);
                return false;
            }

            try
            {
                // Get database reference
                database = cosmosClient.GetDatabase(dbName);
            }
            catch (Exception e)
            {
                LogError(e.Message);
                return false;
            }

            try
            {
                // Create container if not exists
                cosmosContainer = await database!.CreateContainerIfNotExistsAsync(
                    id: containerName,
                    partitionKeyPath: partitionKey,
                    throughput: 400
                );

                // Perform a read test
                cosmosContainer.GetHashCode();

                Log($"\n(Connected to CosmosDB) {cosmosClient.Endpoint}", ConsoleColor.Yellow);
                return true;
            }
            catch (CosmosException e)
            {
                LogError(e.GetType().ToString());
                LogError(
                    "Error Connecting to CosmosDB - check appsettings.json, " +
                    "endpoint or key may be expired"
                );
                return false;
            }
            catch (HttpRequestException e)
            {
                LogError(e.GetType().ToString());
                LogError(
                    "Error Connecting to CosmosDB - check firewall and internet status"
                );
                return false;
            }
        }

        // UpsertProduct()
        // ---------------
        // Takes a scraped Product, transforms it to a DBProduct, and then tries to upsert to CosmosDB.
        public async static Task<UpsertResponse> TransformAndUpsertProduct(Product scrapedProduct)
        {
            System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.NotFound;
            DBProduct? dbProduct = null;

            try
            {
                // Check if product already exists on CosmosDB, throws exception if not found
                var response = await cosmosContainer!.ReadItemAsync<DBProduct>(
                    scrapedProduct.id,
                    new PartitionKey(scrapedProduct.category)   // try use category partition
                );
                statusCode = response.StatusCode;
                dbProduct = response.Resource;
            }
            catch
            {
                // just continue onto the next check
            }
            try
            {
                // Partition key mismatch - try querying by id across all partitions
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.id = @id"
                )
                .WithParameter("@id", scrapedProduct.id);

                var feedIterator = cosmosContainer!.GetItemQueryIterator<DBProduct>(query);

                if (feedIterator.HasMoreResults)
                {
                    var response = await feedIterator.ReadNextAsync();
                    if (response.Count > 0)
                    {
                        dbProduct = response.First();
                        statusCode = System.Net.HttpStatusCode.OK;
                    }
                }
            }
            catch
            {
                // just continue onto the next check
            }

            if (statusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    // Build an updated product with values from both the DB and scraped products
                    ProductResponse productResponse = BuildUpdatedProduct
                    (
                        dbProduct!,
                        scrapedProduct
                    );

                    // Upsert the updated product back to CosmosDB
                    await cosmosContainer!.UpsertItemAsync(
                        productResponse.dbProduct,
                        new PartitionKey(dbProduct!.category)
                    );

                    // Return the UpsertResponse based on what data has changed
                    return productResponse.upsertResponse;
                }
                catch
                {

                }
            }
            else if (statusCode == System.Net.HttpStatusCode.NotFound)
            {
                return await InsertNewProduct(scrapedProduct);
            }

            // Return failed if this part is ever reached
            return UpsertResponse.Failed;
        }

        // BuildUpdatedProduct()
        // --------------------
        // Builds a product with combined data from scrapedProduct, and price history data from dbProduct.

        public static ProductResponse BuildUpdatedProduct(DBProduct dbProduct, Product scrapedProduct)
        {
            // Measure the price difference between the new scraped product and the old db product
            float priceDifference = Math.Abs(dbProduct.priceHistory.Last().price - scrapedProduct.currentPrice);

            // Check if price has changed by more than $0.05
            bool priceHasChanged = priceDifference > 0.05;

            // If price has changed and not on the same day, we can do a full update from the scraped product
            if (priceHasChanged && dbProduct.priceHistory.Last().date != today)
            {
                // Price has changed, so we can create an updated priceHistory with today's addition
                List<DatedPrice> updatedHistory = dbProduct.priceHistory.ToList<DatedPrice>();
                DatedPrice newDatedPriceEntry = new DatedPrice(date: today, price: scrapedProduct.currentPrice);
                updatedHistory.Add(newDatedPriceEntry);

                // Log price change with different verb and colour depending on price change direction
                bool priceTrendingDown = scrapedProduct.currentPrice < dbProduct.priceHistory.Last().price;
                string priceTrendText = "  Price " + (priceTrendingDown ? "Down " : "Up   ") + ":";

                Log(
                    $"{priceTrendText} {dbProduct.name.PadRight(51).Substring(0, 51)} | " +
                    $"${dbProduct.priceHistory.Last().price} > ${scrapedProduct.currentPrice}",
                    priceTrendingDown ? ConsoleColor.Green : ConsoleColor.Red
                );

                // Return new product with updated data
                return new ProductResponse(
                    UpsertResponse.PriceUpdated,
                    new DBProduct(
                        id: dbProduct.id,
                        name: dbProduct.name,
                        size: dbProduct.size,
                        category: dbProduct.category,
                        sourceSite: dbProduct.sourceSite,
                        priceHistory: updatedHistory.ToArray(),
                        lastChecked: today,
                        unitPrice: dbProduct.unitPrice
                    )
                );
            }
            // else if (otherDataHasChanged)
            // {
            //     // If only non-price data has changed, update non price/date fields
            //     return new ProductResponse(UpsertResponse.NonPriceUpdated, new Product(
            //         dbProduct.id,
            //         scrapedProduct.name,
            //         scrapedProduct.size,
            //         dbProduct.currentPrice,
            //         scrapedProduct.category,
            //         scrapedProduct.sourceSite,
            //         dbProduct.priceHistory,
            //         dbProduct.lastUpdated,
            //         scrapedProduct.lastChecked,
            //         scrapedProduct.unitPrice,
            //         scrapedProduct.unitName,
            //         scrapedProduct.originalUnitQuantity
            //     ));
            // }
            else
            {
                // Else existing DB Product has not changed, update only lastChecked
                return new ProductResponse(
                    UpsertResponse.AlreadyUpToDate,
                    dbProduct with { lastChecked = today }
                );
            }
        }

        // InsertNewProduct()
        // ------------------
        // Inserts a new Product into CosmosDB
        private static async Task<UpsertResponse> InsertNewProduct(Product scrapedProduct)
        {
            try
            {
                // Build a new DBProduct with a single priceHistory [ entry ]
                DBProduct newProduct = new DBProduct
                (
                    id: scrapedProduct.id,
                    name: scrapedProduct.name,
                    size: scrapedProduct.size,
                    category: scrapedProduct.category,
                    sourceSite: scrapedProduct.sourceSite,
                    priceHistory: [new DatedPrice(date: today, price: scrapedProduct.currentPrice)],
                    lastChecked: today,
                    unitPrice: scrapedProduct.unitPrice
                );

                await cosmosContainer!.UpsertItemAsync(newProduct, new PartitionKey(newProduct.category));

                Log(
                    $"  New Product: {newProduct.id,-8} | " +
                    $"{newProduct.name!.PadRight(40).Substring(0, 40)}" +
                    $" | $ {scrapedProduct.currentPrice,5} | {newProduct.size}"
                );

                return UpsertResponse.NewProduct;
            }
            catch (CosmosException e)
            {
                Log($"  CosmosDB: Upsert Error for new Product: {e.StatusCode}");
                return UpsertResponse.Failed;
            }
        }
    }
}