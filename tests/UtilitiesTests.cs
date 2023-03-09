using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Scraper.Utilities;

namespace ScraperTests
{
    [TestClass]
    public class UtilitiesTests
    {
        [TestMethod]
        public void DeriveCategoryFromUrl_ArrayLengthForUncategorised()
        {
            string url = "asdf";
            var result = DeriveCategoryFromUrl(url, "/category/");
            Assert.AreEqual<int>(result.Length, 1);
        }

        [TestMethod]
        public void DeriveCategoryFromUrl_UncategorisedValue()
        {
            string url = "asdf";
            var result = DeriveCategoryFromUrl(url, "/category/");
            Assert.AreEqual<string>(result, "Uncategorised");
        }

        [TestMethod]
        public void DeriveCategoryFromUrl_ExcludesQueryParameters()
        {
            string url = "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1&asdf=123f";
            var result = DeriveCategoryFromUrl(url, "/category/");
            Assert.AreEqual<string>(result, "fresh-milk");
        }

        [TestMethod]
        public void DeriveCategoryFromUrl_GetsCorrectCategories()
        {
            string url =
                "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1";
            var result = DeriveCategoryFromUrl(url, "/category/");
            Assert.AreEqual<string>(result, "fresh-milk");
        }

        [TestMethod]
        public void DeriveCategoryFromUrl_WorksWithoutHttpSlash()
        {
            string url = "www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1";
            var result = DeriveCategoryFromUrl(url, "/category/");
            Assert.AreEqual<string>(result, "fresh-milk");
        }

        [TestMethod]
        public void ExtractProductSize_1kg()
        {
            string productName = "Anchor Blue Milk Powder 1kg";
            Assert.AreEqual<string>(ExtractProductSize(productName), "1kg");
        }

        [TestMethod]
        public void ExtractProductSize_255g()
        {
            string productName = "Lee Kum Kee Panda Oyster Sauce 255g";
            Assert.AreEqual<string>(ExtractProductSize(productName), "255g");
        }

        [TestMethod]
        public void ExtractProductSize_NoSize()
        {
            string productName = "Anchor Blue Milk Powder";
            Assert.AreEqual<string>(ExtractProductSize(productName), "");
        }

        [TestMethod]
        public void ExtractProductSize_400ml()
        {
            string productName = "Trident Premium Coconut Cream 400ml";
            Assert.AreEqual<string>(ExtractProductSize(productName), "400ml");
        }
    }
}