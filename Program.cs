using Microsoft.Playwright;

public class PakScraper
{
    public record Product(string name, string id, string imgUrl, float currentPrice, string size);

    public static async Task Main()
    {
        // const string url = "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery?pg=1";
        const string url = "https://www.paknsave.co.nz/shop/category/chilled-frozen-and-desserts?pg=1";
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = false,
        };

        // Launch browser
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);
        var page = await browser.NewPageAsync();

        // Load Page and wait for product grid to load in
        try
        {
            Console.WriteLine("--- Loading Page: " + url);
            await page.GotoAsync(url);
            await page.WaitForSelectorAsync("div.fs-product-card");
        }
        catch (System.Exception)
        {
            Console.WriteLine("- Unable to load web page");
            return;
        }

        // Get list of all product card entries
        var query = await page.QuerySelectorAllAsync("div.fs-product-card");
        var productCardList = query.ToList();

        // Prepare a table to log to console
        Console.WriteLine("--- " + productCardList.Count + " product entries found \n");
        Console.WriteLine(
            " ID".PadRight(11) +
            "Title".PadRight(34) + "\t" +
            "Size".PadRight(10) + "\t" +
            "Price");
        Console.WriteLine("".PadRight(70).Replace(" ", "-"));

        // Loop through each product entry
        foreach (var item in productCardList)
        {
            string? title, linkRelativeUrl, imgUrl, size, dollarString, centString, id;
            float currentPrice;

            // Assign scraped values
            var aTag = await item.QuerySelectorAsync("a");
            title = await aTag!.GetAttributeAsync("aria-label");
            linkRelativeUrl = await aTag.GetAttributeAsync("href");

            var imgDiv = await aTag.QuerySelectorAsync("div div");
            imgUrl = await imgDiv!.GetAttributeAsync("data-src-s");

            var imageFilename = imgUrl!.Split("/").Last();  // get original ID from image url
            id = "P-" + imageFilename.Split(".").First();   // prepend P- to ID

            var pTag = await aTag.QuerySelectorAsync("p");
            size = await pTag!.InnerHTMLAsync();
            size = size.Replace("l", "L");  // capitialize L for litres

            var dollarSpan = await item.QuerySelectorAsync(".fs-price-lockup__dollars");
            dollarString = await dollarSpan!.InnerHTMLAsync();

            var centSpan = await item.QuerySelectorAsync(".fs-price-lockup__cents");
            centString = await centSpan!.InnerHTMLAsync();

            currentPrice = float.Parse(dollarString + "." + centString);

            string logEntry =
                id! + "   " +
                title!.PadRight(34).Substring(0, 34) + "\t" +
                size!.PadRight(10).Substring(0, 10) + "\t" +
                "$" + currentPrice + "\t";

            Console.WriteLine(logEntry);
        }

        Console.WriteLine("\n--- Scraping Completed \n");

        await browser.CloseAsync();

        return;
    }
}
