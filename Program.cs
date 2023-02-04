using Microsoft.Playwright;

public class PakScraper
{
    public record Product(string name, string id, string imgUrl, float currentPrice, string size);

    public static async Task Main()
    {
        string[] urls = new string[] {
        "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery?pg=1",
        "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts?pg=1"
        };

        // Launch browser without headless option
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false }
        );
        var page = await browser.NewPageAsync();

        // Open up each URL and run the scraping function
        await openAllUrlsForScraping(urls, page);

        // Complete after all URLs have been scraped
        Console.WriteLine("\n--- Scraping Completed \n");
        await browser.CloseAsync();
        return;
    }

    async static Task openAllUrlsForScraping(string[] urls, IPage page)
    {
        int urlIndex = 1;

        foreach (var url in urls)
        {
            // Try load page and wait for product cards to dynamically load in
            try
            {
                Console.WriteLine($"--- Loading Page [{urlIndex++}/{urls.Count()}] " + url);
                await page.GotoAsync(url);
                await page.WaitForSelectorAsync("div.fs-product-card");
            }
            catch (System.Exception)
            {
                Console.WriteLine("- Unable to load web page");
                return;
            }

            // Query list of all product card entries
            var allProductElements = await page.QuerySelectorAllAsync("div.fs-product-card");
            //var productEntryElements = query.ToList();

            // Prepare a table to log to console
            Console.WriteLine("--- " + allProductElements.Count + " product entries found");
            Console.WriteLine(
                " ID".PadRight(11) +
                "Title".PadRight(34) + "\t" +
                "Size".PadRight(10) + "\t" +
                "Price");
            Console.WriteLine("".PadRight(70).Replace(" ", "-"));

            foreach (var element in allProductElements)
            {
                Product scrapedProduct = await scrapeProductElementToRecord(element);
                // Send to CosmosDB

                // Print a log for each product
                string logEntry =
                    scrapedProduct.id + "   " +
                    scrapedProduct.name!.PadRight(34).Substring(0, 34) + "\t" +
                    scrapedProduct.size!.PadRight(10).Substring(0, 10) + "\t" +
                    "$" + scrapedProduct.currentPrice + "\t";
                Console.WriteLine(logEntry);
            }

            // This URL has now completed scraping. A delay is added in-between each subsequent URL
            Thread.Sleep(31000);
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
        string id = "P-" + imageFilename.Split(".").First();   // prepend P- to ID

        // Size
        var pTag = await aTag.QuerySelectorAsync("p");
        string size = await pTag!.InnerHTMLAsync();
        size = size.Replace("l", "L");  // capitialize L for litres

        // Price
        var dollarSpan = await element.QuerySelectorAsync(".fs-price-lockup__dollars");
        string dollarString = await dollarSpan!.InnerHTMLAsync();
        var centSpan = await element.QuerySelectorAsync(".fs-price-lockup__cents");
        string centString = await centSpan!.InnerHTMLAsync();
        float currentPrice = float.Parse(dollarString + "." + centString);

        // Return completed Product record
        return (new Product(name!, id, imgUrl, currentPrice, size));
    }
}
