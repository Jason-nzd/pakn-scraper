using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Playwright;
using static PakScraper.Program;

namespace PakScraperTests
{
    [TestClass]
    public class ScraperTests
    {
        [TestMethod]
        public async void Playwright_Connected()
        {
            // Launch Playwright Browser in headless mode
            var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true }
            );
            Assert.IsTrue(browser.IsConnected);
        }

        [TestMethod]
        public void DeriveCategoriesFromUrl_ArrayLengthForUncategorised()
        {
            string badurl = "asdf";
            Assert.AreEqual<int>(DeriveCategoriesFromUrl(badurl).Length, 1);
        }

        [TestMethod]
        public void DeriveCategoriesFromUrl_UncategorisedValue()
        {
            string badurl = "asdf";
            Assert.AreEqual<string>(DeriveCategoriesFromUrl(badurl)[0], "Uncategorised");
        }

        [TestMethod]
        public void DeriveCategoriesFromUrl_ExcludesQueryParameters()
        {
            string hasQueryParameters = "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1&asdf=123f";
            var result = DeriveCategoriesFromUrl(hasQueryParameters);
            Assert.IsTrue(result.SequenceEqual(new string[] { "fresh-milk" }));
        }

        [TestMethod]
        public void DeriveCategoriesFromUrl_GetsCorrectCategories()
        {
            string normalUrl =
                "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1";
            Assert.IsTrue(DeriveCategoriesFromUrl(normalUrl)[0] == "fresh-milk");
        }

        [TestMethod]
        public void DeriveCategoriesFromUrl_WorksWithoutHttpSlash()
        {
            string nohttp = "www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1";
            Assert.IsTrue(DeriveCategoriesFromUrl(nohttp)[0] == "fresh-milk");
        }
    }
}