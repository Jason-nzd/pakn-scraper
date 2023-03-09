using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Playwright;
using static Scraper.Program;

namespace ScraperTests
{
    [TestClass]
    public class ScraperTests
    {
        [TestMethod]
        public async void EstablishPlaywright_BrowserConnected()
        {
            // Singletons for Playwright
            IPlaywright? playwright = null;
            IPage? playwrightPage = null;
            IBrowser? browser = null;

            await EstablishPlaywright();
            Assert.IsTrue(browser!.IsConnected);
        }

        [TestMethod]
        public async void EstablishPlaywright_GoogleConnected()
        {
            // Singletons for Playwright
            IPlaywright? playwright = null;
            IPage? playwrightPage = null;
            IBrowser? browser = null;

            await EstablishPlaywright();
            await playwrightPage!.GotoAsync("http://www.google.com");
            Assert.IsNotNull(playwrightPage);

        }
    }
}