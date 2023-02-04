using Microsoft.Playwright;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync();
var page = await browser.NewPageAsync();

await page.GotoAsync("https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery?pg=1");

var cards = page.Locator("div.fs-product-card");

var html = cards.InnerHTMLAsync();
var query = await page.QuerySelectorAllAsync("div.fs-product-card");
var l = query.ToList();

// public record Product(string name, string id, float currentPrice, string size);

Console.WriteLine("Hello, World!");
Console.WriteLine(html.ToString());