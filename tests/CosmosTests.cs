using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Scraper.CosmosDB;
using static Scraper.Program;

namespace ScraperTests
{
    [TestClass]
    public class CosmosTests
    {
        [TestMethod]
        public void BuildUpdatedProduct_SameData()
        {
            // Create sample product existing in database
            float oldPrice = 3.65f;
            DateTime oldDate = DateTime.UtcNow.AddDays(-20);
            DatedPrice oldDatedPrice = new DatedPrice(oldDate, oldPrice);

            Product dbProduct = new Product("1234", "milk", "2l", oldPrice, new string[] { "milk" }, "site",
                new DatedPrice[] { oldDatedPrice }, oldDate, oldDate);

            // Create sample product scraped days later, but with the same scraped data
            float newPrice = 3.65f;
            DateTime newDate = DateTime.UtcNow.AddDays(4);
            DatedPrice newDatedPrice = new DatedPrice(newDate, newPrice);

            Product scrapedProduct = new Product("1234", "milk", "2l", newPrice, new string[] { "milk" }, "site",
                new DatedPrice[] { newDatedPrice }, newDate, newDate);

            // Assert that correct UpsertResponse is returned
            Assert.IsTrue(BuildUpdatedProduct(dbProduct, scrapedProduct).upsertResponse == UpsertResponse.AlreadyUpToDate);
        }

        [TestMethod]
        public void BuildUpdatedProduct_PriceHistoryCountIncrease()
        {
            // Create sample product existing in database
            float oldPrice = 3.65f;
            DateTime oldDate = DateTime.UtcNow.AddDays(-20);
            DatedPrice oldDatedPrice = new DatedPrice(oldDate, oldPrice);

            Product dbProduct = new Product("1234", "milk", "2l", oldPrice, new string[] { "milk" }, "site",
                new DatedPrice[] { oldDatedPrice }, oldDate, oldDate);

            // Create sample product scraped days later, with increased price
            float newPrice = 5.20f;
            DateTime newDate = DateTime.UtcNow.AddDays(4);
            DatedPrice newDatedPrice = new DatedPrice(newDate, newPrice);

            Product scrapedProduct = new Product("1234", "milk", "2l", newPrice, new string[] { "milk" }, "site",
                new DatedPrice[] { newDatedPrice }, newDate, newDate);

            Product updatedProduct = BuildUpdatedProduct(dbProduct, scrapedProduct).product;

            // Assert that the price history array length has increased by one
            Assert.IsTrue(updatedProduct.priceHistory.Count() == dbProduct.priceHistory.Count() + 1,
                "Updated/DB Price Count = " + updatedProduct.priceHistory.Count() + " / " + dbProduct.priceHistory.Count());
        }

        [TestMethod]
        public void BuildUpdatedProduct_PriceChanged()
        {
            // Create sample product existing in database
            float oldPrice = 3.65f;
            DateTime oldDate = DateTime.UtcNow.AddDays(-20);
            DatedPrice oldDatedPrice = new DatedPrice(oldDate, oldPrice);

            Product dbProduct = new Product("1234", "milk", "2l", oldPrice, new string[] { "milk" }, "site",
                new DatedPrice[] { oldDatedPrice }, oldDate, oldDate);

            // Create sample product scraped days later, with increased price
            float newPrice = 5.20f;
            DateTime newDate = DateTime.UtcNow.AddDays(4);
            DatedPrice newDatedPrice = new DatedPrice(newDate, newPrice);

            Product scrapedProduct = new Product("1234", "milk", "2l", newPrice, new string[] { "milk" }, "site",
                new DatedPrice[] { newDatedPrice }, newDate, newDate);

            Product updatedProduct = BuildUpdatedProduct(dbProduct, scrapedProduct).product;

            // Assert that currentPrice and lastUpdated were correctly set
            Assert.AreEqual<float>(updatedProduct.currentPrice, newPrice, updatedProduct.currentPrice + " - " + newPrice);
            Assert.AreEqual<DateTime>(updatedProduct.lastUpdated, newDate);
        }

        [TestMethod]
        public async void EstablishConnection_Exception()
        {
            try
            {
                await EstablishConnection("asdf", "asdf", "asdf");
            }
            catch (Exception e)
            {
                StringAssert.Contains(e.Message, "appsettings.json");
            }
        }
    }
}
