using System.Text.RegularExpressions;
using static Scraper.Program;

namespace Scraper
{
    public partial class Utilities
    {
        // Parses urls and optimises query options for best results, returns null if invalid
        public static string? ParseAndOptimiseURL(
            string url,
            string urlShouldContain = ".co.nz",
            string replaceQueryParams = "")
        {
            // If string contains desired string, such as .co.nz, it should be a URL
            if (url.Contains(urlShouldContain))
            {
                string cleanURL = url;

                // If url contains ? it has query options already set
                if (url.Contains('?'))
                {
                    // Strip any existing query options off of URL
                    cleanURL = url.Substring(0, url.IndexOf('?'));
                }
                // Replace query parameters with optimised ones,
                //  such as limiting to certain sellers,
                //  or showing a higher number of products
                cleanURL += replaceQueryParams;

                // Return cleaned url
                return cleanURL;
            }
            else return null;
        }

        // UploadImageUsingRestAPI() - sends an image url to an Azure Function API to be uploaded
        public async static Task UploadImageUsingRestAPI(string imgUrl, Product product)
        {
            // Get AZURE_FUNC_URL from appsettings.json
            // Example format:
            // https://<func-app-name>.azurewebsites.net/api/ImageToS3?code=<func-auth-code>
            string? funcUrl = config!.GetSection("AZURE_FUNC_URL").Value;

            // Check funcUrl is valid
            if (!funcUrl!.Contains("http"))
                throw new Exception("AZURE_FUNC_URL in appsettings.json invalid. Should be in format:\n\n" +
                "\"AZURE_FUNC_URL\": \"https://<func-app-name>.azurewebsites.net/api/ImageToS3?code=<func-auth-code>\"");

            // Perform http get
            string restUrl = funcUrl + "&destination=s3://supermarketimages/product-images/" + product.id + "&source=" + imgUrl;
            var response = await httpclient.GetAsync(restUrl);
            var responseMsg = await response.Content.ReadAsStringAsync();

            // Log for successful upload of new image
            if (responseMsg.Contains("S3 Upload of Full-Size and Thumbnail WebPs"))
            {
                Log(
                    ConsoleColor.Gray,
                    $"  New Image  : {product.id.PadLeft(9)} | {product.name.PadRight(50).Substring(0, 50)}"
                );
            }
            else if (responseMsg.Contains("already exists"))
            {
                // Do not log for existing images
            }
            else
            {
                // Log any other errors that may have occurred
                Console.Write(restUrl + "\n" + responseMsg);
            }
            return;
        }

        // Reads lines from a txt file, then return as a List
        public static List<string>? ReadLinesFromFile(string fileName)
        {
            try
            {
                List<string> result = new List<string>();
                string[] lines = File.ReadAllLines(@fileName);

                if (lines.Length == 0) throw new Exception("No lines found in " + fileName);

                foreach (string line in lines)
                {
                    if (line != null) result.Add(line);
                }

                return result;
            }
            catch (System.Exception)
            {
                LogError("Unable to read file " + fileName + "\n");
                return null;
            }
        }

        // Extract potential product size from product name
        // 'Anchor Blue Milk Powder 1kg' returns '1kg'
        public static string ExtractProductSize(string productName)
        {
            // \s = whitespace char, \d = digit, \w+ = 1 more word chars, $ = end
            string pattern = @"\s\d\w+$";

            string result = "";
            result = Regex.Match(productName, pattern).ToString().Trim();
            return result;
        }

        // Derives category name from url by taking the last /bracket/
        // www.domain.co.nz/c/food-pets-household/food-drink/pantry/milk-bread/milk
        // returns milk
        public static string DeriveCategoryFromUrl(string url, string urlMustContain)
        {
            // If url doesn't contain a section such as /food-drink/, return Uncategorised
            if (url.IndexOf(urlMustContain) >= 0)
            {
                int categoriesStartIndex = url.IndexOf(urlMustContain);
                int categoriesEndIndex = url.Contains("?") ? url.IndexOf("?") : url.Length;
                string categoriesString =
                    url.Substring(
                        categoriesStartIndex,
                        categoriesEndIndex - categoriesStartIndex
                    );
                string lastCategory = categoriesString.Split("/").Last();

                return lastCategory;
            }
            else return "Uncategorised";
        }

        // Shorthand function for logging with provided colour
        public static void Log(ConsoleColor color, string text)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }

        // Shorthand function for logging with red colour
        public static void LogError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}