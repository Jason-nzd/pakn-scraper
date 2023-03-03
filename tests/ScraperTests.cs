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
            string hasQueryParameters = "https://www.thewarehouse.co.nz/c/food-pets-household/food-drink/pantry/canned-food?asdfr=gfd";
            var result = DeriveCategoriesFromUrl(hasQueryParameters);
            Assert.IsTrue(result.SequenceEqual(new string[] { "canned-food" }));
        }

        [TestMethod]
        public void DeriveCategoriesFromUrl_GetsCorrectCategories()
        {
            string normalUrl =
                "https://www.thewarehouse.co.nz/c/food-pets-household/food-drink/pantry/ingredients-sauces-oils/table-sauces";
            Assert.IsTrue(DeriveCategoriesFromUrl(normalUrl)[0] == "table-sauces");
        }

        [TestMethod]
        public void DeriveCategoriesFromUrl_WorksWithoutHttpSlash()
        {
            string nohttp = "www.thewarehouse.co.nz/c/food-pets-household/food-drink/pantry/canned-food";
            Assert.IsTrue(DeriveCategoriesFromUrl(nohttp)[0] == "canned-food");
        }
    }
}