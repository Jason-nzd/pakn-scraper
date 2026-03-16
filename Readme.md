# PaknSave Scraper

Scrapes product pricing and info from the PaknSave NZ website and logs the data to the console.

Product information and price snapshots can be optionally stored into Azure CosmosDB.
Images can be sent to an API for resizing, analysis and other processing.

The scraper is powered by `Microsoft Playwright`, requiring `.NET 8.0` & `Powershell` to run.

## Quick Setup

With `.NET SDK` installed, clone this repo, change directory into `/src`, then restore and build .NET packages with:

```powershell
dotnet restore
dotnet build
```

Playwright Chromium web browser can be installed using:

```cmd
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

The program is now ready to use and will scrape all default URLs in `Urls.txt`.

```cmd
dotnet run
```

## Sample Dry Run Output

```cmd
       ID | Name                                    | Size   | Price  | Unit Price
----------------------------------------------------------------------------------
 P1234567 | Coconut Supreme Slice                   | 350g   | $ 5.89 | $16.83 /kg
 P5345284 | Gluten Free Delicious Choc Chip Cookie  | 250g   | $ 4.89 | $19.56 /kg
 P5678287 | Gluten Free Delicious Macadamia Cookie  | 250g   | $ 4.89 | $19.56 /kg
 P3457825 | Belgium Slice                           | Each   | $ 5.89 | 
 P5789285 | Gluten Free Delicious Double Choc Chip  | 250g   | $ 4.89 | $19.56 /kg
 P2356288 | Bakery Crunchy Bran With Sultanas       | 230g   | $ 4.49 | $19.52 /kg
 P2765307 | Sanniu Evergreen Variant Biscuits       | 4x132g | $ 6.36 | $12.05 /kg
```

## Optional Setup with appsettings.json

To set optional advanced parameters, edit `appsettings.json`.

If writing to CosmosDB, set the CosmosDB variables with the format:

```json
{
  "COSMOS_ENDPOINT": "<your cosmosdb endpoint uri>",
  "COSMOS_KEY": "<your cosmosdb primary key>",
  "COSMOS_DB": "<cosmosdb database>",
  "COSMOS_CONTAINER": "<cosmosdb container>",
}
```

To override the default store location with a specific location, set geolocation co-ordinates in Long/Lat format. Long/Lat co-ordinates can be obtained from resources such as google maps.
The closest store location to the co-ordinates will be selected when running the scraper.

```json
{
    "GEOLOCATION_LONG": "174.91",
    "GEOLOCATION_LAT": "-41.21",
}
```

Images can be sent off to a REST API for processing with:
```json
{
  "IMAGE_PROCESS_API_URL": "<rest api url>",
}
```

## Command-Line Usage

To dry run the scraper and log each product to the console:

```powershell
dotnet run
```

Optional arguments can be mixed and match for advanced usage:

To store scraped data to the cosmosdb database (defined in appsettings).

```powershell
dotnet run db
```

Images can be sent off for processing to an API (defined in appsettings).
```powershell
dotnet run db images
```

The browser defaults to headless mode but can changed to headed for debugging and reliability.
```powershell
dotnet run headed
```

## Sample Product Stored in CosmosDB

This sample was re-run on multiple days to capture changing prices.

```json
{
    "id": "P1234567",
    "name": "Supreme Lite Milk",
    "size": "1L",
    "category": "milk",
    "sourceSite": "paknsave.co.nz",
    "priceHistory": [
        {
            "date": "2023-07-02",
            "price": 2.95
        },
        {
            "date": "2024-12-30",
            "price": 3.25
        },
        {
            "date": "2025-06-30",
            "price": 3.4
        },
        {
            "date": "2025-12-29",
            "price": 3.85
        }
    ],
    "lastChecked": "2026-01-04",
    "unitPrice": "3.31/L"
}
```
