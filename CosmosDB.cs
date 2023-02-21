using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using static PakScraper.Program;

namespace PakScraper
{
    public partial class CosmosDB
    {
        public static async Task<Boolean> EstablishConnection(
            string databaseName,
            string partitionKey,
            string containerName
        )
        {
            try
            {
                // Read from appsettings.json or appsettings.local.json
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                cosmosClient = new CosmosClient(
                    accountEndpoint: config.GetRequiredSection("COSMOS_ENDPOINT").Get<string>(),
                    authKeyOrResourceToken: config.GetRequiredSection("COSMOS_KEY").Get<string>()!
                );

                database = cosmosClient.GetDatabase(id: databaseName);

                // Container reference with creation if it does not already exist
                cosmosContainer = await database.CreateContainerIfNotExistsAsync(
                    id: containerName,
                    partitionKeyPath: partitionKey,
                    throughput: 400
                );

                Log(ConsoleColor.Yellow, $"\n(Connected to CosmosDB) {cosmosClient.Endpoint}");
                return true;
            }
            catch (Microsoft.Azure.Cosmos.CosmosException e)
            {
                Log(ConsoleColor.Red, e.GetType().ToString());
                Log(ConsoleColor.Red,
                "Error Connecting to CosmosDB - check appsettings.json, endpoint or key may be expired");
                return false;
            }
            catch (HttpRequestException e)
            {
                Log(ConsoleColor.Red, e.GetType().ToString());
                Log(ConsoleColor.Red,
                "Error Connecting to CosmosDB - check firewall and internet status");
                return false;
            }
            catch (Exception e)
            {
                Log(ConsoleColor.Red, e.GetType().ToString());
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

        // Takes a scraped Product, and tries to insert or update an existing Product on CosmosDB
        public async static Task<UpsertResponse> UpsertProduct(Product scrapedProduct)
        {
            bool productAlreadyOnCosmosDB = false;
            Product? dbProduct = null;

            try
            {
                // Check if product already exists on CosmosDB, throws exception if not found
                var response = await cosmosContainer!.ReadItemAsync<Product>(
                    scrapedProduct.id,
                    new PartitionKey(scrapedProduct.name)
                );

                // Set local product from CosmosDB resource
                dbProduct = response.Resource;
                if (response.StatusCode == System.Net.HttpStatusCode.OK) productAlreadyOnCosmosDB = true;
            }
            catch (Microsoft.Azure.Cosmos.CosmosException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                    productAlreadyOnCosmosDB = false;
            }

            if (productAlreadyOnCosmosDB)
            {
                // Check if price has changed
                float dbPrice = dbProduct!.currentPrice;
                float scrapedPrice = scrapedProduct.currentPrice;

                if (dbPrice != scrapedPrice)
                {
                    // Price has changed, so we can create an updated Product with the changes
                    DatedPrice[] updatedHistory = dbProduct.priceHistory;
                    updatedHistory.Append(scrapedProduct.priceHistory[0]);

                    Product updatedProduct = new Product(
                        dbProduct.id,
                        dbProduct.name,
                        scrapedProduct.currentPrice,
                        dbProduct.category,
                        dbProduct.size,
                        dbProduct.sourceSite,
                        updatedHistory,
                        dbProduct.imgUrl
                    );

                    // Log price change with different verb and colour depending on price change direction
                    bool priceTrendingDown = (scrapedPrice < dbPrice);
                    string priceTrendText = "Price " + (priceTrendingDown ? "Decreased" : "Increased") + ":";

                    Log(priceTrendingDown ? ConsoleColor.Green : ConsoleColor.Red,
                        $"{priceTrendText} {dbProduct.name.PadRight(40).Substring(0, 40)} from " +
                        $"${dbProduct.currentPrice} to ${scrapedProduct.currentPrice}"
                    );

                    try
                    {
                        // Upsert the updated product back to CosmosDB
                        await cosmosContainer!.UpsertItemAsync<Product>(updatedProduct, new PartitionKey(updatedProduct.name));
                        return UpsertResponse.Updated;
                    }
                    catch (Microsoft.Azure.Cosmos.CosmosException e)
                    {
                        Console.WriteLine($"CosmosDB Upsert Error on existing Product: {e.StatusCode}");
                        return UpsertResponse.Failed;
                    }
                }
                else
                {
                    return UpsertResponse.AlreadyUpToDate;
                }
            }
            else
            {
                try
                {
                    // No existing product was found, upload to CosmosDB
                    await cosmosContainer!.UpsertItemAsync<Product>(scrapedProduct, new PartitionKey(scrapedProduct.name));

                    Console.WriteLine(
                        $"{"New Product:".PadLeft(15)} {scrapedProduct.id.PadRight(8)} | " +
                        $"{scrapedProduct.name!.PadRight(40).Substring(0, 40)} | {scrapedProduct.size.PadRight(8)}" +
                        $" | $ {scrapedProduct.currentPrice.ToString().PadRight(5)} | {scrapedProduct.category.Last()}"
                    );

                    return UpsertResponse.NewProduct;
                }
                catch (Microsoft.Azure.Cosmos.CosmosException e)
                {
                    Console.WriteLine($"{"CosmosDB:".PadLeft(15)} Upsert Error for new Product: {e.StatusCode}");
                    return UpsertResponse.Failed;
                }
            }
        }
    }
}