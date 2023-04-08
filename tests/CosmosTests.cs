using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Scraper.CosmosDB;
using static Scraper.Program;

namespace ScraperTests
{
    [TestClass]
    public class CosmosTests
    {
        // Sample product already existing in database
        static float oldPrice = 3.65f;
        static DateTime oldDate = DateTime.UtcNow.AddDays(-20);
        static DatedPrice oldDatedPrice = new DatedPrice(oldDate, oldPrice);

        Product dbProduct = new Product("1234", "milk", "2l", oldPrice, new string[] { "milk" }, "site",
            new DatedPrice[] { oldDatedPrice }, oldDate, oldDate, 1.82f, "L", 2);

        // Sample product scraped days later, but with the same scraped data
        static float daysLaterPrice = 3.65f;
        static DateTime daysLaterDate = DateTime.UtcNow.AddDays(4);
        static DatedPrice daysLaterDatedPrice = new DatedPrice(daysLaterDate, daysLaterPrice);

        Product daysLaterProduct = new Product("1234", "milk", "2l", daysLaterPrice, new string[] { "milk" }, "site",
            new DatedPrice[] { daysLaterDatedPrice }, daysLaterDate, daysLaterDate, 1.82f, "L", 2);

        // Sample product scraped days later, with increased price
        static float increasedPrice = 5.20f;
        static DatedPrice increasedDatePrice = new DatedPrice(daysLaterDate, increasedPrice);

        Product scrapedProduct = new Product("1234", "milk", "2l", increasedPrice, new string[] { "milk" }, "site",
            new DatedPrice[] { increasedDatePrice }, daysLaterDate, daysLaterDate, 2.6f, "L", 2);


        [TestMethod]
        public void BuildUpdatedProduct_SameData()
        {
            var response = BuildUpdatedProduct(dbProduct, daysLaterProduct);
            // Assert that correct UpsertResponse is returned
            Assert.IsTrue(response.upsertResponse == UpsertResponse.AlreadyUpToDate,
                response.upsertResponse.ToString() + " - " + response.product.currentPrice + " - " + dbProduct.currentPrice);
        }

        [TestMethod]
        public void BuildUpdatedProduct_PriceHistoryCountIncrease()
        {
            Product updatedProduct = BuildUpdatedProduct(dbProduct, scrapedProduct).product;

            // Assert that the price history array length has increased by one
            Assert.IsTrue(updatedProduct.priceHistory.Count() == dbProduct.priceHistory.Count() + 1,
                "Updated/DB Price Count = " + updatedProduct.priceHistory.Count() + " / " + dbProduct.priceHistory.Count());
        }

        [TestMethod]
        public void BuildUpdatedProduct_PriceChanged()
        {
            Product updatedProduct = BuildUpdatedProduct(dbProduct, scrapedProduct).product;

            // Assert that currentPrice and lastUpdated were correctly set
            Assert.AreEqual<float>(updatedProduct.currentPrice, increasedPrice, updatedProduct.currentPrice + " - " + increasedPrice);
            Assert.AreEqual<DateTime>(updatedProduct.lastUpdated, daysLaterDate);
        }

        [TestMethod]
        public async Task EstablishConnection_Exception()
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
