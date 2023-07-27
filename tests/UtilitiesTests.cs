using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Scraper.Utilities;

namespace ScraperTests
{
    [TestClass]
    public class UtilitiesTests
    {
        [TestMethod]
        public void DeriveCategoryFromURL_ExcludesQueryParameters()
        {
            string url = "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1&asdf=123f";
            var result = DeriveCategoryFromURL(url);
            Assert.AreEqual<string>(result, "fresh-milk");
        }

        [TestMethod]
        public void DeriveCategoryFromURL_GetsCorrectCategories()
        {
            string url =
                "https://www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1";
            var result = DeriveCategoryFromURL(url);
            Assert.AreEqual<string>(result, "fresh-milk");
        }

        [TestMethod]
        public void DeriveCategoryFromURL_WorksWithoutHttpSlash()
        {
            string url = "www.paknsave.co.nz/shop/category/fresh-foods-and-bakery/dairy--eggs/fresh-milk?pg=1";
            var result = DeriveCategoryFromURL(url);
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

        [TestMethod]
        public void DeriveUnitPriceString_2L()
        {
            string? unitPriceString = DeriveUnitPriceString("Bottle 2L", 6.5f);
            Assert.AreEqual<string>(unitPriceString, "3.25/L/2", unitPriceString);
        }

        [TestMethod]
        public void DeriveUnitPriceNoodles()
        {
            string? unitPriceString = DeriveUnitPriceString("72g each 5pack", 4.5f);
            Assert.AreEqual<string>(unitPriceString, "12.5/g/360", unitPriceString);
        }

        [TestMethod]
        public void DeriveUnitPriceString_Multiplier()
        {
            string? unitPriceString = DeriveUnitPriceString("Pouch 4 x 107mL", 6.5f);
            Assert.AreEqual<string>(unitPriceString, "15.19/L/428", unitPriceString);
        }

        [TestMethod]
        public void DeriveUnitPriceString_Decimal()
        {
            string? unitPriceString = DeriveUnitPriceString("Bottle 1.5L", 3f);
            Assert.AreEqual<string>(unitPriceString, "2/L/1.5", unitPriceString);
        }

        [TestMethod]
        public void DeriveUnitPriceString_SimpleKg()
        {
            string? unitPriceString = DeriveUnitPriceString("kg", 3f);
            Assert.AreEqual<string>(unitPriceString, "3/kg/1", unitPriceString);
        }

        [TestMethod]
        public void GetOverriddenProductSize_Match()
        {
            string productSize = GetOverriddenProductSize("P5022829", "10 pack");
            Assert.AreEqual<string>(productSize, "800g");
        }

        [TestMethod]
        public void GetOverridenProductSize_NoMatch()
        {
            string productSize = GetOverriddenProductSize("P501234", "10 pack");
            Assert.AreEqual<string>(productSize, "10 pack");
        }
    }
}