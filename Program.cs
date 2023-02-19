using Microsoft.Playwright;
//using Microsoft.Extensions.Configuration;

public class PakScraper
{
    static bool dryRunMode = true;
    static int secondsDelayBetweenPageScrapes = 22;

    static string[] urls = new string[] {
        "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery?pg=1",
        "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts?pg=1"
    };

    public record DatedPrice(string date, float price);
    public record Product(string id, string name, float currentPrice, string size, string sourceSite, DatedPrice[] priceHistory, string imgUrl);

    enum UpsertResponse
    {
        NewProduct,
        Updated,
        AlreadyUpToDate,
        Failed
    }

    public static async Task Main(string[] args)
    {
        // Handle arguments - 'dotnet run dry' will run in dry mode
        if (args.Length > 0)
        {
            if (args[0] == "dry") dryRunMode = true;
        }

        // If dry run mode on, skip CosmosDB
        if (dryRunMode) log(ConsoleColor.Yellow, $"\n(Dry Run mode on)");

        // Launch Playwright Browser
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false }
        );
        var page = await browser.NewPageAsync();

        // Define excluded types and urls to reject
        string[] typeExclusions = { "image", "stylesheet", "media", "font", "other" };
        string[] urlExclusions = { "googleoptimize.com", "gtm.js", "visitoridentification.js", "js-agent.newrelic.com", "challenge" };
        List<string> exclusions = urlExclusions.ToList<string>();

        // Route with exclusions processed
        await page.RouteAsync("**/*", async route =>
        {
            var req = route.Request;
            bool excludeThisRequest = false;
            string trimmedUrl = req.Url.Length > 120 ? req.Url.Substring(0, 120) + "..." : req.Url;

            foreach (string exclusion in exclusions)
            {
                if (req.Url.Contains(exclusion)) excludeThisRequest = true;
            }

            if (excludeThisRequest)
            {
                //log(ConsoleColor.Red, $"{req.Method} {req.ResourceType} - {trimmedUrl}");
                await route.AbortAsync();
            }
            else
            {
                //log(ConsoleColor.White, $"{req.Method} {req.ResourceType} - {trimmedUrl}");
                await route.ContinueAsync();
            }
        });

        // Open up each URL and run the scraping function
        await openEachURLForScraping(urls, page);

        // Complete after all URLs have been scraped
        log(ConsoleColor.Blue, "\nScraping Completed \n");

        // Clean up playwright browser and end program
        await browser.CloseAsync();
        return;
    }

    async static Task openEachURLForScraping(string[] urls, IPage page)
    {
        int urlIndex = 1;

        foreach (var url in urls)
        {
            // Try load page and wait for full page to dynamically load in
            try
            {
                log(ConsoleColor.Yellow, $"\nLoading Page [{urlIndex++}/{urls.Count()}] {url.PadRight(112).Substring(12, 100)}");
                await page.GotoAsync(url);

                await page.WaitForSelectorAsync("span.fs-price-lockup__cents");
            }
            catch (System.Exception e)
            {
                log(ConsoleColor.Red, "Unable to Load Web Page");
                Console.Write(e.ToString());
                return;
            }

            // Query all product card entries
            var productElements = await page.QuerySelectorAllAsync("div.fs-product-card");
            Console.WriteLine(productElements.Count.ToString().PadLeft(15) + " products found");

            // Loop through every found playwright element
            foreach (var element in productElements)
            {
                // Create Product object from playwright element
                Product scrapedProduct = await scrapeProductElementToRecord(element);

                // In Dry Run mode, print a log row for every product
                Console.WriteLine(
                    scrapedProduct.id.PadLeft(9) + " | " +
                    scrapedProduct.name!.PadRight(50).Substring(0, 50) + " | " +
                    scrapedProduct.size.PadRight(12) + " | " +
                    "$" + scrapedProduct.currentPrice
                );
            }

            // This page has now completed scraping. A delay is added in-between each subsequent URL
            if (urlIndex <= urls.Count())
            {
                Console.WriteLine($"{"Waiting".PadLeft(15)} {secondsDelayBetweenPageScrapes}s until next page scrape..");
                Thread.Sleep(secondsDelayBetweenPageScrapes * 1000);
            }
        }

        // All page URLs have completed scraping.
        return;
    }

    // Takes a playwright element "div.fs-product-card", scrapes each of the desired data fields,
    //  and then returns a completed Product record
    async static Task<Product> scrapeProductElementToRecord(IElementHandle element)
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
            log(ConsoleColor.Red, $"Price scrape error on {name}");
            Console.Write(e);
        }
        // Return completed Product record
        return (new Product(id, name!, currentPrice, size, sourceSite, priceHistory, imgUrl));
    }

    // Shorthand function for logging with colour
    static void log(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = ConsoleColor.White;
    }
}
