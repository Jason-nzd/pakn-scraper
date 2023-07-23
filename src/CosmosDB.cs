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

        public static async Task<bool> EstablishConnection(string db, string partitionKey, string container)
        {
            try
            {
                // Read from appsettings.json or appsettings.local.json
                cosmosClient = new CosmosClient(
                    accountEndpoint: config!.GetRequiredSection("COSMOS_ENDPOINT").Get<string>(),
                    authKeyOrResourceToken: config!.GetRequiredSection("COSMOS_KEY").Get<string>()!
                );

                database = cosmosClient.GetDatabase(id: db);

                cosmosContainer = await database.CreateContainerIfNotExistsAsync(
                    id: container,
                    partitionKeyPath: partitionKey,
                    throughput: 400
                );

                Log(ConsoleColor.Yellow, $"\n(Connected to CosmosDB) {cosmosClient.Endpoint}");
                return true;
            }
            catch (CosmosException e)
            {
                LogError(e.GetType().ToString());
                Log(ConsoleColor.Red,
                "Error Connecting to CosmosDB - check appsettings.json, endpoint or key may be expired");
                return false;
            }
            catch (HttpRequestException e)
            {
                LogError(e.GetType().ToString());
                Log(ConsoleColor.Red,
                "Error Connecting to CosmosDB - check firewall and internet status");
                return false;
            }
            catch (Exception e)
            {
                LogError(e.GetType().ToString());
                Log(ConsoleColor.Red,
                "Error Connecting to CosmosDB - make sure appsettings.json is created and contains:");
                Log(ConsoleColor.White,
                    "{\n" +
                    "\t\"COSMOS_ENDPOINT\": \"<your cosmosdb endpoint uri>\",\n" +
                    "\t\"COSMOS_KEY\": \"<your cosmosdb primary key>\"\n" +
                    "}\n"
                );
                return false;
            }
        }

        // Takes a scraped Product, and tries to insert it or update it on CosmosDB
        public async static Task<UpsertResponse> UpsertProduct(Product scrapedProduct)
        {
            try
            {
                // Check if product already exists on CosmosDB, throws exception if not found
                var response = await cosmosContainer!.ReadItemAsync<Product>(
                    scrapedProduct.id,
                    new PartitionKey(scrapedProduct.name)
                );

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // Get product from CosmosDB resource
                    Product dbProduct = response.Resource;

                    // Build an updated product with values from both the DB and scraped products
                    ProductResponse productResponse = BuildUpdatedProduct(dbProduct, scrapedProduct);

                    // Upsert the updated product back to CosmosDB
                    await cosmosContainer!.UpsertItemAsync(
                        productResponse.product,
                        new PartitionKey(productResponse.product.name)
                    );

                    // Return the UpsertResponse based on what data has changed
                    return productResponse.upsertResponse;
                }
            }
            // Catch not found exception and prepare to upload a new Product
            catch (CosmosException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return (await InsertNewProduct(scrapedProduct));
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
            }

            // Return failed if this part is ever reached
            return UpsertResponse.Failed;
        }

        // Builds a new product with new data from scrapedProduct, and price history data from dbProduct
        public static ProductResponse BuildUpdatedProduct(Product dbProduct, Product scrapedProduct)
        {
            // Measure the price difference between the new scraped product and the old db product
            float priceDifference = Math.Abs(dbProduct.currentPrice - scrapedProduct.currentPrice);

            // Check if price has changed by more than $0.05
            bool priceHasChanged = priceDifference > 0.05;

            // Check if category or size has changed
            string oldCategories = string.Join(" ", dbProduct.category);
            string newCategories = string.Join(" ", scrapedProduct.category);
            bool otherDataHasChanged =
                dbProduct!.size != scrapedProduct.size ||
                oldCategories != newCategories ||
                dbProduct.sourceSite != scrapedProduct.sourceSite ||
                dbProduct.name != scrapedProduct.name ||
                dbProduct.unitPrice != scrapedProduct.unitPrice ||
                dbProduct.unitName != scrapedProduct.unitName ||
                dbProduct.originalUnitQuantity != scrapedProduct.originalUnitQuantity
            ;

            // If price has changed and not on the same day, we can update it
            if (priceHasChanged &&
                dbProduct.lastUpdated.ToShortDateString() !=
                scrapedProduct.lastUpdated.ToShortDateString()
            )
            {
                // Price has changed, so we can create an updated Product with the changes
                List<DatedPrice> updatedHistory = dbProduct.priceHistory.ToList<DatedPrice>();
                updatedHistory.Add(scrapedProduct.priceHistory[0]);

                // Log price change with different verb and colour depending on price change direction
                bool priceTrendingDown = scrapedProduct.currentPrice < dbProduct!.currentPrice;
                string priceTrendText = "  Price " + (priceTrendingDown ? "Down " : "Up   ") + ":";

                Log(priceTrendingDown ? ConsoleColor.Green : ConsoleColor.Red,
                    $"{priceTrendText} {dbProduct.name.PadRight(40).Substring(0, 40)} | " +
                    $"${dbProduct.currentPrice} > ${scrapedProduct.currentPrice}"
                );

                // Return new product with updated data
                return new ProductResponse(UpsertResponse.PriceUpdated, new Product(
                    dbProduct.id,
                    scrapedProduct.name,
                    scrapedProduct.size,
                    scrapedProduct.currentPrice,
                    scrapedProduct.category,
                    scrapedProduct.sourceSite,
                    updatedHistory.ToArray(),
                    scrapedProduct.lastUpdated,
                    scrapedProduct.lastChecked,
                    scrapedProduct.unitPrice,
                    scrapedProduct.unitName,
                    scrapedProduct.originalUnitQuantity
                ));
            }
            else if (otherDataHasChanged)
            {
                // If only non-price data has changed, update non price/date fields
                return new ProductResponse(UpsertResponse.NonPriceUpdated, new Product(
                    dbProduct.id,
                    scrapedProduct.name,
                    scrapedProduct.size,
                    dbProduct.currentPrice,
                    scrapedProduct.category,
                    scrapedProduct.sourceSite,
                    dbProduct.priceHistory,
                    dbProduct.lastUpdated,
                    scrapedProduct.lastChecked,
                    scrapedProduct.unitPrice,
                    scrapedProduct.unitName,
                    scrapedProduct.originalUnitQuantity
                ));
            }
            else
            {
                // Else existing DB Product has not changed, update only lastChecked
                return new ProductResponse(UpsertResponse.AlreadyUpToDate, new Product(
                    dbProduct.id,
                    dbProduct.name,
                    dbProduct.size,
                    dbProduct.currentPrice,
                    dbProduct.category,
                    dbProduct.sourceSite,
                    dbProduct.priceHistory,
                    dbProduct.lastUpdated,
                    scrapedProduct.lastChecked,
                    dbProduct.unitPrice,
                    dbProduct.unitName,
                    dbProduct.originalUnitQuantity
                ));
            }
        }

        public enum UpsertResponse
        {
            NewProduct,
            PriceUpdated,
            NonPriceUpdated,
            AlreadyUpToDate,
            Failed
        }

        public struct ProductResponse
        {
            public UpsertResponse upsertResponse;
            public Product product;

            public ProductResponse(UpsertResponse upsertResponse, Product product) : this()
            {
                this.upsertResponse = upsertResponse;
                this.product = product;
            }
        }

        // Inserts a new Product into CosmosDB
        private static async Task<UpsertResponse> InsertNewProduct(Product scrapedProduct)
        {
            try
            {
                // No existing product was found, upload to CosmosDB
                await cosmosContainer!.UpsertItemAsync(scrapedProduct, new PartitionKey(scrapedProduct.name));

                Console.WriteLine(
                    $"  New Product: {scrapedProduct.id.PadRight(8)} | " +
                    $"{scrapedProduct.name!.PadRight(40).Substring(0, 40)}" +
                    $" | $ {scrapedProduct.currentPrice.ToString().PadLeft(5)} | {scrapedProduct.category.Last()}"
                );

                return UpsertResponse.NewProduct;
            }
            catch (CosmosException e)
            {
                Console.WriteLine($"  CosmosDB: Upsert Error for new Product: {e.StatusCode}");
                return UpsertResponse.Failed;
            }
        }

        public static async Task CustomQuery()
        {
            var feedIterator = cosmosContainer!.GetItemQueryIterator<Product>(
                "select * from products p where contains(p.id, 'M')"
            );

            while (feedIterator.HasMoreResults)
            {
                foreach (var item in await feedIterator.ReadNextAsync())
                {
                    Console.WriteLine($"  Deleting {item.id} - {item.name}");
                    await cosmosContainer.DeleteItemAsync<Product>(item.id, new PartitionKey(item.name));
                }
            }
        }
    }
}