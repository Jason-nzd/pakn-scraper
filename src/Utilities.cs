using System.Text.RegularExpressions;
using static Scraper.Program;

namespace Scraper
{
    // Struct for manual overriding scraped product size and category found in 'ProductOverrides.txt'
    public struct SizeAndCategoryOverride
    {
        public string size;
        public string category;

        public SizeAndCategoryOverride(string size, string category)
        {
            this.size = size;
            this.category = category;
        }
    }

    // Struct for parsing URLs and additional data stored in 'Urls.txt'
    public struct CategorisedURL
    {
        public string url;
        public string[] categories;
        public int numPages;

        public CategorisedURL(string url, string[] categories, int numPages)
        {
            this.url = url;
            this.categories = categories;
            this.numPages = numPages;
        }
    }

    public partial class Utilities
    {
        // ParseLineToCategorisedURL()
        // ---------------------------
        // Parses a textLine containing a url and optional overridden category names.
        // Returns a CategorisedURL object or null if the line is invalid.

        public static CategorisedURL? ParseLineToCategorisedURL(
            string textLine,
            string urlShouldContain = ".co.nz",
            string replaceQueryParamsWith = "")
        {
            // Get url from the first section
            string url = textLine.Split(' ').First();

            // If url doesn't contain desired section, return null
            if (!url.Contains(urlShouldContain)) return null;

            // Optimise query parameters
            url = OptimiseURLQueryParameters(url, replaceQueryParamsWith);

            // Derive default product category from url
            string[] categories = { DeriveCategoryFromURL(url) };

            // Set default numPages to scrape
            int numPages = 1;

            // Loop through any parameters placed after the url
            string[] textLineParams = textLine.Split(' ');
            for (int i = 1; i < textLineParams.Length; i++)
            {
                string param = textLineParams[i];

                // If overridden categories are provided, override the derived categories
                if (param.Contains("categories"))
                {
                    categories = param.Replace("categories=", "").Split(",");
                }

                // Set numPages if specified
                if (param.Contains("pages"))
                {
                    int parsedNumPages = int.Parse(param.Replace("pages=", ""));

                    // Ensure numPages is within reasonable range
                    if (parsedNumPages >= 1 && parsedNumPages <= 20)
                    {
                        numPages = parsedNumPages;
                    }
                }
            }

            return new CategorisedURL(url, categories, numPages);
        }

        // OptimiseURLQueryParameters
        // --------------------------
        // Parses urls and optimises query options for best results
        // Returns null if invalid

        public static string OptimiseURLQueryParameters(string url, string replaceQueryParamsWith)
        {
            string cleanURL = url;

            // If url contains 'search?', keep all query parameters
            if (url.ToLower().Contains("search?"))
            {
                return url;
            }

            // Else strip all query parameters
            else if (url.Contains('?'))
            {
                cleanURL = url.Substring(0, url.IndexOf('?')) + "?";
            }

            // If there were no existing query parameters, ensure a ? is added
            else cleanURL += "?";

            // Replace query parameters with optimised ones,
            //  such as limiting to certain sellers,
            //  or showing a higher number of products
            cleanURL += replaceQueryParamsWith;

            // Return cleaned url
            return cleanURL;
        }

        // UploadImageUsingRestAPI()
        // -------------------------
        // Sends an image url to an Azure Function API, where it will be uploaded.

        public async static Task UploadImageUsingRestAPI(string imgUrl, Product product)
        {
            // Get AZURE_FUNC_URL from appsettings.json
            // Example format:
            // https://<func-app-name>.azurewebsites.net/api/ImageToS3?code=<func-auth-code>&destination=s3://<bucket>/<optional-path>/
            string? funcUrl = config!.GetSection("AZURE_FUNC_URL").Value;

            // Check funcUrl is valid
            if (!funcUrl!.Contains("http"))
                throw new Exception("AZURE_FUNC_URL in appsettings.json invalid. Should be in format:\n\n" +
                "\"AZURE_FUNC_URL\": \"https://<func-app-name>.azurewebsites.net/api/ImageToS3?code=<func-auth-code>&destination=s3://<bucket>/<optional-path>/\"");

            // Perform http get
            string restUrl = funcUrl + product.id + "&source=" + imgUrl;
            var response = await httpclient.GetAsync(restUrl);
            var responseMsg = await response.Content.ReadAsStringAsync();

            // Log for successful upload of new image
            if (responseMsg.Contains("S3 Upload of Full-Size and Thumbnail WebPs"))
            {
                Log(
                    ConsoleColor.Gray,
                    $"  New Image  : {product.id.PadLeft(8)} | {product.name.PadRight(50).Substring(0, 50)}"
                );
            }
            else if (responseMsg.Contains("already exists"))
            {
                // Do not log for existing images
            }
            else if (responseMsg.Contains("greyscale"))
            {
                Log(ConsoleColor.Gray, $"  Image {product.id} is greyscale, skipping...");
            }
            else
            {
                // Log any other errors that may have occurred
                Console.Write(restUrl + "\n" + responseMsg);
            }
            return;
        }

        // IsValidProduct()
        // ----------------
        // Validates product values are within reasonable ranges

        public static bool IsValidProduct(Product product)
        {
            try
            {
                if (product.name.Length < 4 || product.name.Length > 100) return false;
                if (product.id.Length < 2 || product.id.Length > 20) return false;
                if (
                  product.currentPrice <= 0 || product.currentPrice > 999
                )
                {
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ReadLinesFromFile()
        // -------------------
        // Reads lines from a txt file, then returns as a List

        public static List<string>? ReadLinesFromFile(string fileName)
        {
            try
            {
                List<string> result = new List<string>();
                string[] lines = File.ReadAllLines(@fileName);

                if (lines.Length == 0) throw new Exception("No lines found in " + fileName);

                foreach (string line in lines)
                {
                    if (line != null && !line.StartsWith("#")) result.Add(line.Trim());
                }

                return result;
            }
            catch (System.Exception)
            {
                LogError("Unable to read file " + fileName + "\n");
                return null;
            }
        }

        // ExtractProductSize()
        // --------------------
        // Extract potential product size from product name
        // 'Anchor Blue Milk Powder 1kg' returns '1kg'

        public static string ExtractProductSize(string productName)
        {
            // \b = word boundary, \d+ = 1 or more digits, \. period, 
            // (g|kg|l|ml) = any of these words, \s = whitespace, ? optional

            string name = productName.ToLower();

            // First try match 4 x 40ml style multiplied units
            string multiplierPattern = @"\d+\s?x\s?\d+\s?(g|kg|l|ml)\b";
            string multiplierResult =
                Regex.Match(name, multiplierPattern).ToString();

            if (multiplierResult.Length > 0)
            {
                // Split by 'x' into '4' and '40ml' sections
                int packSize = int.Parse(Regex.Match(multiplierResult.Split('x')[0], @"\d+").ToString());
                int quantity = int.Parse(Regex.Match(multiplierResult.Split('x')[1], @"\d+").ToString());

                // Get unit name 
                string unitName = Regex.Match(multiplierResult, @"(g|kg|l|ml)").ToString().ToLower();

                // Parse to int and multiply together
                float total = packSize * quantity;

                // If units are in grams, normalize quantity and convert to /kg
                if (unitName == "g")
                {
                    total = total / 1000;
                    unitName = "kg";
                }

                // If units are in mL, normalize quantity and convert to /L
                if (unitName == "ml")
                {
                    total = total / 1000;
                    unitName = "L";
                }

                if (unitName == "l") unitName = unitName.Replace("l", "L");

                // Return original name '4 x 40ml' as '160ml'
                return Math.Round(total, 2) + unitName;
            }

            // Try match '100ml 24pack' or '24 pack 100ml' style names
            string packPattern = @"(\d+\s?(l|ml)\s\d+\s?pack\b|\d+\s?pack\s\d+\s?(l|ml))";
            string packResult =
                Regex.Match(name, packPattern).ToString();

            if (packResult.Length > 0)
            {
                // Match 24 pack and parse to int
                string packSizeString = Regex.Match(packResult, @"\d+\s?pack").ToString();
                int packSize = int.Parse(packSizeString.Replace("pack", "").Trim());

                // Match 100ml quantity
                string quantityString = Regex.Match(packResult, @"\d+\s?(l|ml)").ToString();
                int quantity = int.Parse(Regex.Match(quantityString, @"\d+").ToString());

                // Get unit name
                string unitName = Regex.Match(packResult, @"(l|ml)").ToString();

                // Parse to int and multiply together
                float total = packSize * quantity;

                // If units are in mL, normalize quantity and convert to /L
                if (unitName == "ml")
                {
                    total = total / 1000;
                    unitName = "L";
                }

                if (unitName == "l") unitName = unitName.Replace("l", "L");

                // Return original name '100ml 24pack' as '2.4L'
                return Math.Round(total, 2) + unitName;
            }

            // Try match ordinary styled names such as Milk 350ml
            string pattern = @"\d+(\.\d+)?(g|kg|l|ml)\b";

            string result = Regex.Match(productName.ToLower(), pattern).ToString().Trim();

            return result.Replace("l", "L").Replace("mL", "ml");
        }

        // DeriveCategoryFromURL()
        // -----------------------
        // Derives category name from url by taking the last /bracket/
        // 'www.domain.co.nz/c/food-pets-household/food-drink/pantry/milk-bread/milk'
        // returns 'milk'

        public static string DeriveCategoryFromURL(string url)
        {
            int categoriesEndIndex = url.Contains("?") ? url.IndexOf("?") : url.Length;
            string categoriesString =
                url.Substring(
                    0,
                    categoriesEndIndex
                );
            string lastCategory = categoriesString.Split("/").Last();
            return lastCategory;
        }

        // CheckProductOverrides()
        // ---------------------------
        // Checks a txt file to see if the product should use a manually overridden values.
        // Returns a SizeAndCategoryOverride object

        public static SizeAndCategoryOverride CheckProductOverrides(string id)
        {
            List<string> overrideLines = ReadLinesFromFile("ProductOverrides.txt")!;

            string sizeOverrideFound = "";
            string categoryOverrideFound = "";

            foreach (string line in overrideLines)
            {
                string[] splitLine = line.Trim().Split(' ');

                // Check if 1st section matches product ID
                if (splitLine[0] == id)
                {
                    // Then loop through any additional sections
                    for (int i = 1; i < splitLine.Length; i++)
                    {
                        // If any section matches weight/size/volume symbols, use this size override
                        if (Regex.IsMatch(splitLine[i].ToLower(), @"\d+(g|kg|ml|l)"))
                        {
                            sizeOverrideFound = splitLine[i];
                        }

                        // Override any categories if found
                        if (splitLine[i].Contains("category="))
                        {
                            categoryOverrideFound = splitLine[i].Replace("category=", "");
                        }
                    }
                }
            }
            return new SizeAndCategoryOverride(sizeOverrideFound, categoryOverrideFound);
        }

        // DeriveUnitPriceString()
        // -----------------------
        // Derives unit quantity, unit name, and price per unit of a product,
        // Returns a string in format 450/ml

        public static string? DeriveUnitPriceString(string productSize, float productPrice)
        {
            // Return early if productSize is blank
            if (productSize == null || productSize.Length < 2) return null;

            string? matchedUnit = null;
            float? quantity = null;
            float? originalUnitQuantity = null;

            // If size is simply 'kg', process it as 1kg
            if (productSize == "kg" || productSize == "per kg")
            {
                quantity = 1;
                matchedUnit = "kg";
                originalUnitQuantity = 1;
            }
            else
            {
                // MatchedUnit is derived from product size, 450ml = ml
                matchedUnit = string.Join("", Regex.Matches(productSize.ToLower(), @"(g|kg|ml|l)\b"));

                // Quantity is derived from product size, 450ml = 450
                // Can include decimals, 1.5kg = 1.5
                try
                {
                    string quantityMatch = string.Join("", Regex.Matches(productSize, @"(\d|\.)"));
                    quantity = float.Parse(quantityMatch);
                    originalUnitQuantity = quantity;
                }
                catch (System.Exception)
                {
                    // If quantity cannot be parsed, the function will return null
                }
            }

            if (matchedUnit.Length > 0 && quantity > 0)
            {
                // Handle edge case where size contains a 'multiplier x sub-unit' - eg. 4 x 107mL
                string matchMultipliedSizeString = Regex.Match(productSize, @"\d+\s?x\s?\d+").ToString();
                if (matchMultipliedSizeString.Length > 2)
                {
                    int multiplier = int.Parse(matchMultipliedSizeString.Split("x")[0].Trim());
                    int subUnitSize = int.Parse(matchMultipliedSizeString.Split("x")[1].Trim());
                    quantity = multiplier * subUnitSize;
                    originalUnitQuantity = quantity;
                    matchedUnit = matchedUnit.ToLower().Replace("x", "");
                    //Log(ConsoleColor.DarkGreen, productSize + " = (" + quantity + ") (" + matchedUnit + ")");
                }

                // Handle edge case where size is in format '72g each 5pack'
                matchMultipliedSizeString = Regex.Match(productSize, @"\d+(g|ml)\seach\s\d+pack").ToString();
                if (matchMultipliedSizeString.Length > 2)
                {
                    int multiplier = int.Parse(matchMultipliedSizeString.Split("each")[1].Trim());
                    int subUnitSize = int.Parse(matchMultipliedSizeString.Split("each")[0].Trim());
                    quantity = multiplier * subUnitSize;
                    originalUnitQuantity = quantity;
                    matchedUnit = matchedUnit.ToLower().Replace("each", "");
                    //Log(ConsoleColor.DarkGreen, productSize + " = (" + quantity + ") (" + matchedUnit + ")");
                }

                // If units are in grams, normalize quantity and convert to /kg
                if (matchedUnit == "g")
                {
                    quantity = quantity / 1000;
                    matchedUnit = "kg";
                }

                // If units are in mL, normalize quantity and convert to /L
                if (matchedUnit == "ml")
                {
                    quantity = quantity / 1000;
                    matchedUnit = "L";
                }

                // Capitalize L for Litres
                if (matchedUnit == "l") matchedUnit = "L";

                // Set per unit price, rounded to 2 decimal points
                string roundedUnitPrice = Math.Round((decimal)(productPrice / quantity), 2).ToString();
                //Console.WriteLine(productPrice + " / " + quantity + " = " + roundedUnitPrice + "/" + matchedUnit);

                // Return in format '450g cheese' = '0.45/kg/450'
                return roundedUnitPrice + "/" + matchedUnit + "/" + originalUnitQuantity;
            }
            return null;
        }

        // Log()
        // -----
        // Shorthand function for logging with provided colour

        public static void Log(ConsoleColor color, string text)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }

        // LogError()
        // ----------
        // Shorthand function for logging with red colour
        public static void LogError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}