# Pak'n'Save Scraper

Scrapes product pricing and info from The Pak'n'Save NZ website. Price snapshots can be saved to Azure CosmosDB, or this program can simply log to console. Images can be saved to AWS S3.

Requires .NET 6 SDK & Powershell. Azure CosmosDB and AWS S3 are optional.

## Setup

First clone this repo, then restore and build .NET packages with:

```powershell
dotnet restore && dotnet build
```

Playwright Chromium web browser must be downloaded and installed using:

```cmd
pwsh bin/Debug/net6.0/playwright.ps1 install chromium
```

If running in dry mode, the program is now ready to use.

If using CosmosDB, create `appsettings.json` containing the endpoint and key using the format:

```json
{
  "COSMOS_ENDPOINT": "<your cosmosdb endpoint uri>",
  "COSMOS_KEY": "<your cosmosdb primary key>"
  "<todo: also set aws s3 keys>"
}
```

## Usage

To dry run the scraper, logging each product to the console:

```powershell
dotnet run dry
```

To run the scraper and save each product to the database:

```powershell
dotnet run
```

## Sample Dry Run Output

```cmd
P5003287 | Mainland Mild & Creamy Edam Cheese       | 1kg      | $16.99 | cheese-blocks
P5013472 | Mainland Smooth & Creamy Colby Cheese    | 1kg      | $16.99 | cheese-blocks
P5003259 | Mainland Tasty Aged Cheddar Cheese       | 250g     | $ 6.69 | cheese-blocks
P5007407 | Rolling Meadow Tasty Cheese              | 800g     | $15.99 | cheese-blocks
P5027950 | Mainland Tasty Aged Cheddar Cheese       | 500g     | $12.79 | cheese-blocks
```

## Sample Product Stored in CosmosDB

```json
{
    "id": "P5003259",
    "name": "Mainland Tasty Aged Cheddar Cheese",
    "currentPrice": 6.69,
    "category": [
        "chilled-frozen-and-desserts",
        "cheese",
        "cheese-blocks"
    ],
    "size": "250g",
    "sourceSite": "paknsave.co.nz",
    "priceHistory": [
        {
            "date": "Wed Feb 22 2023",
            "price": 6.69
        }
        {
            "date": "Wed Feb 14 2023",
            "price": 7.99
        }
    ],
}
```
