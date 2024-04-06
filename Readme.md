# PaknSave Scraper

Scrapes product pricing and info from the PaknSave NZ website. Product information and price snapshots can be stored on Azure CosmosDB, or this program can simply log to console. Images can be sent to an API for resizing, analysis and other processing.

The scraper is powered by `Microsoft Playwright`. It requires `.NET 6 SDK` & `Powershell` to run. Azure CosmosDB and image processing are optional.

## Quick Setup

First clone or download this repo, change directory into `/src`, then restore and build .NET packages with:

```powershell
dotnet restore && dotnet build
```

Playwright Chromium web browser must be downloaded and installed using:

```cmd
pwsh bin/Debug/net6.0/playwright.ps1 install chromium
```

The program is now ready to use and will scrape all URLs placed in `Urls.txt`.

```cmd
dotnet run
```

## Advanced Setup with appsettings.json

To set optional advanced parameters, create `appsettings.json`.

If using CosmosDB, set the CosmosDB endpoint and key using the format:

```json
{
  "COSMOS_ENDPOINT": "<your cosmosdb endpoint uri>",
  "COSMOS_KEY": "<your cosmosdb primary key>"
}
```

To override the default store location with a specific location, set geolocation co-ordinates in Long/Lat format. Long/Lat co-ordinates can be obtained from resources such as google maps.
The closest store location to the co-ordinates will be selected when running the scraper.

```json
{
    "GEOLOCATION_LAT": "-41.21",
    "GEOLOCATION_LONG": "174.91"
}
```

## Command-Line Usage

To dry run the scraper, logging each product to the console:

```powershell
dotnet run
```

To run the scraper with both logging and storing of each product to the database:

```powershell
dotnet run db
```

## Sample Dry Run Output

```cmd
 P1234567 | Coconut Supreme Slice                           | 350g     | $ 5.89 | $16.83 /kg
 P5345284 | Cookies Gluten Free Delicious Choc Chip Cookie  | 250g     | $ 4.89 | $19.56 /kg
 P5678287 | Cookies Gluten Free Delicious Macadamia Cookie  | 250g     | $ 4.89 | $19.56 /kg
 P3457825 | Belgium Slice                                   | Each     | $ 5.89 | 
 P5789285 | Cookies Gluten Free Delicious Double Choc Chip  | 250g     | $ 4.89 | $19.56 /kg
 P2356288 | Bakery Crunchy Bran Biscuits With Sultanas      | 230g     | $ 4.49 | $19.52 /kg
 P2765307 | Sanniu Evergreen Variant Biscuits               | 4 x 132g | $ 6.36 | $12.05 /kg
```

## Sample Product Stored in CosmosDB

This sample was re-run on multiple days to capture changing prices.

```json
{
    "id": "P1234567",
    "name": "Coconut Supreme Slice",
    "size": "350g",
    "currentPrice": 5.89,
    "category": [
        "biscuits"
    ],
    "priceHistory": [
        {
            "date": "2023-05-04T01:00:00",
            "price": 5.89
        }
        {
            "date": "2023-01-02T01:00:00",
            "price": 5.49
        }
    ],
    "unitPrice": 16.83,
    "unitName": "kg",
}
```
