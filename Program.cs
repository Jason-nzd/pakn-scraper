using Microsoft.Playwright;
using Microsoft.Azure.Cosmos;

namespace PakScraper
{
    public class Program
    {
        private static int secondsDelayBetweenPageScrapes = 32;
        private static string[] urls = new string[] {
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/fruit--vegetables/fresh-fruit?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/fruit--vegetables/fresh-vegetables?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/fruit--vegetables/prepacked-fresh-fruit?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/fruit--vegetables/prepacked-fresh-vegetables?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/eggs?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/long-life-milk--milk-powder?pg=1",
            "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/dairy--lactose-free?pg=1",
            "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts?pg=1",
            "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts/cheese/cheese-blocks?pg=1",
            "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts/desserts/ice-cream--frozen-yoghurt?pg=1",
            "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts/frozen-foods/frozen-fries--potatoes?pg=1",
            "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts/frozen-foods/frozen-beef-lamb--pork?pg=1",
            "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts/frozen-foods/frozen-chicken--poultry?pg=1",
            "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts/frozen-foods/frozen-pizza--bases?pg=1",
            "https://www.paknsave.co.nz/shop/category/pantry/confectionery?pg=1",
            "https://www.paknsave.co.nz/shop/category/pantry/confectionery/chocolate-blocks?pg=1",
            "https://www.paknsave.co.nz/shop/category/pets/pet-supplies/cat-food?pg=1",
            "https://www.paknsave.co.nz/shop/category/pets/pet-supplies/cat-treats?pg=1",
        };

        public record Product(
            string id,
            string name,
            float currentPrice,
            string[] category,
            string size,
            string sourceSite,
            DatedPrice[] priceHistory,
            string imgUrl
        );

        public record DatedPrice(
            string date,
            float price
        );

        public static CosmosClient? cosmosClient;
        public static Database? database;
        public static Container? cosmosContainer;
        public static IPage? playwrightPage;

        public static async Task Main(string[] args)
        {
            // Handle arguments - 'dotnet run dry' will run in dry mode, bypassing CosmosDB
            if (args.Length > 0)
            {
                if (args[0] == "dry") dryRunMode = true;
                Log(ConsoleColor.Yellow, $"\n(Dry Run mode on)");
            }

            // Launch Playwright Browser
            var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = false }
            );
            playwrightPage = await browser.NewPageAsync();
            await RoutePlaywrightExclusions(logToConsole: false);

            // Connect to CosmosDB - end program if unable to connect
            if (!dryRunMode)
            {
                if (!await CosmosDB.EstablishConnection(
                       databaseName: "supermarket-prices",
                       partitionKey: "/name",
                       containerName: "supermarket-products"
                   )) return;
            }

            // Connect to AWS S3
            if (!dryRunMode)
            {
                S3.EstablishConnection(bucketName: "paknsaveimages");
            }

            // Open up each URL and run the scraping function
            for (int i = 0; i < urls.Count(); i++)
            {
                // Try load page and wait for full content to dynamically load in
                try
                {
                    Log(ConsoleColor.Yellow,
                        $"\nLoading Page [{i + 1}/{urls.Count()}] {urls[i].PadRight(112).Substring(12, 100)}");
                    await playwrightPage!.GotoAsync(urls[i]);
                    await playwrightPage.WaitForSelectorAsync("span.fs-price-lockup__cents");
                }
                catch (System.Exception e)
                {
                    Log(ConsoleColor.Red, "Unable to Load Web Page");
                    Console.Write(e.ToString());
                    return;
                }

                // Query all product card entries
                var productElements = await playwrightPage.QuerySelectorAllAsync("div.fs-product-card");
                Log(ConsoleColor.Yellow, productElements.Count.ToString().PadLeft(5) + " products found");

                // Create counters for logging purposes
                int newProductsCount = 0, updatedProductsCount = 0, upToDateProductsCount = 0;

                // Loop through every found playwright element
                foreach (var element in productElements)
                {
                    // Create Product object from playwright element
                    Product scrapedProduct = await ScrapeProductElementToRecord(element, urls[i]);

                    if (!dryRunMode)
                    {
                        // Try upsert to CosmosDB
                        UpsertResponse response = await CosmosDB.UpsertProduct(scrapedProduct);

                        // Try upload image to AWS S3
                        //await S3.UploadImageToS3(scrapedProduct.imgUrl);

                        // Increment stats counters based on response from CosmosDB
                        switch (response)
                        {
                            case UpsertResponse.NewProduct:
                                newProductsCount++;
                                break;

                            case UpsertResponse.Updated:
                                updatedProductsCount++;
                                break;

                            case UpsertResponse.AlreadyUpToDate:
                                upToDateProductsCount++;
                                break;

                            case UpsertResponse.Failed:
                            default:
                                break;
                        }
                    }
                    else
                    {
                        // In Dry Run mode, print a log row for every product
                        Console.WriteLine(
                            scrapedProduct.id.PadLeft(9) + " | " + scrapedProduct.name!.PadRight(40).Substring(0, 40) +
                            " | " + scrapedProduct.size.PadRight(8) + " | $" +
                            scrapedProduct.currentPrice.ToString().PadLeft(5) + " | " +
                            scrapedProduct.category.Last().PadRight(10)
                        );
                    }
                }

                // Log consolidated CosmosDB stats for entire page scrape
                if (!dryRunMode)
                {
                    Log(ConsoleColor.Blue, $"{"CosmosDB:".PadLeft(12)} {newProductsCount} new products, " +
                    $"{updatedProductsCount} updated, {upToDateProductsCount} already up-to-date");
                }

                // This page has now completed scraping. A delay is added in-between each subsequent URL
                if (i != urls.Count() - 1)
                {
                    Log(ConsoleColor.Gray,
                        $"{"Waiting".PadLeft(10)} {secondsDelayBetweenPageScrapes}s until next page scrape.."
                    );
                    Thread.Sleep(secondsDelayBetweenPageScrapes * 1000);
                }
            }

            // Clean up playwright browser and end program
            Log(ConsoleColor.Blue, "\nScraping Completed \n");
            await browser.CloseAsync();
            if (!dryRunMode) S3.Dispose();
            return;
        }

        // Takes a playwright element "div.fs-product-card", scrapes each of the desired data fields,
        //  and then returns a completed Product record
        private async static Task<Product> ScrapeProductElementToRecord(IElementHandle element, string sourceUrl)
        {
            // Name
            var aTag = await element.QuerySelectorAsync("a");
            string? name = await aTag!.GetAttributeAsync("aria-label");

            // Image URL
            var imgDiv = await aTag.QuerySelectorAsync("div div");
            string? imgUrl = await imgDiv!.GetAttributeAsync("data-src-s");
            imgUrl = imgUrl!.Replace("200x200", "400x400");     // get the higher-res version

            // ID
            var imageFilename = imgUrl!.Split("/").Last();  // get original ID from image url
            string id = "P" + imageFilename.Split(".").First();   // prepend P to ID

            // Size
            var pTag = await aTag.QuerySelectorAsync("p");
            string size = await pTag!.InnerHTMLAsync();
            size = size.Replace("l", "L");  // capitalize L for litres

            // Mark source website
            string sourceSite = "paknsave.co.nz";

            string[]? categories = DeriveCategoriesFromUrl(sourceUrl);

            // Price scraping is put in try-catch to better handle edge cases
            float currentPrice = 0;
            DatedPrice[] priceHistory = { };

            try
            {
                var dollarSpan = await element.QuerySelectorAsync(".fs-price-lockup__dollars");
                string dollarString = await dollarSpan!.InnerHTMLAsync();

                var centSpan = await element.QuerySelectorAsync(".fs-price-lockup__cents");
                string centString = await centSpan!.InnerHTMLAsync();
                currentPrice = float.Parse(dollarString + "." + centString);

                // DatedPrice with date format 'Tue Jan 14 2023'
                string todaysDate = DateTime.Now.ToString("ddd MMM dd yyyy");
                DatedPrice todaysDatedPrice = new DatedPrice(todaysDate, currentPrice);

                // Create Price History array with a single element
                priceHistory = new DatedPrice[] { todaysDatedPrice };
            }
            catch (Exception e)
            {
                Log(ConsoleColor.Red, $"Price scrape error on {name}");
                Console.Write(e);
            }
            // Return completed Product record
            return (new Product(id, name!, currentPrice, categories!, size, sourceSite, priceHistory, imgUrl));
        }

        // Shorthand function for logging with colour
        public static void Log(ConsoleColor color, string text)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static string[]? DeriveCategoriesFromUrl(string url)
        {
            // www.domain.co.nz/shop/category/chilled-frozen-and-desserts?pg=1"
            // If url doesn't contain /browse/, return no category
            if (url.IndexOf("/category/") < 0) return null;

            int categoriesStartIndex = url.IndexOf("/category/");
            int categoriesEndIndex = url.Contains("?") ? url.IndexOf("?") : url.Length;
            string categoriesString = url.Substring(categoriesStartIndex, categoriesEndIndex - categoriesStartIndex);
            string[] splitCategories = categoriesString.Split("/").Skip(2).ToArray();

            return splitCategories;
        }

        private static async Task RoutePlaywrightExclusions(bool logToConsole)
        {
            // Define excluded types and urls to reject
            string[] typeExclusions = { "image", "stylesheet", "media", "font", "other" };
            string[] urlExclusions = { "googleoptimize.com", "gtm.js", "visitoridentification.js",
                "js-agent.newrelic.com", "challenge-platform" };
            List<string> exclusions = urlExclusions.ToList<string>();

            // Route with exclusions processed
            await playwrightPage!.RouteAsync("**/*", async route =>
            {
                var req = route.Request;
                bool excludeThisRequest = false;
                string trimmedUrl = req.Url.Length > 120 ? req.Url.Substring(0, 120) + "..." : req.Url;

                foreach (string exclusion in exclusions)
                {
                    if (req.Url.Contains(exclusion)) excludeThisRequest = true;
                }
                if (typeExclusions.Contains(req.ResourceType)) excludeThisRequest = true;

                if (excludeThisRequest)
                {
                    if (logToConsole) Log(ConsoleColor.Red, $"{req.Method} {req.ResourceType} - {trimmedUrl}");
                    await route.AbortAsync();
                }
                else
                {
                    if (logToConsole) Log(ConsoleColor.White, $"{req.Method} {req.ResourceType} - {trimmedUrl}");
                    await route.ContinueAsync();
                }
            });
        }

        public enum UpsertResponse
        {
            NewProduct,
            Updated,
            AlreadyUpToDate,
            Failed
        }

        private static bool dryRunMode = false;
    }
}
