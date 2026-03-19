# 🛒 Pak'n'Save Scraper
[![.NET](https://img.shields.io/badge/.NET-8.0+-5C2D91?logo=dotnet)](https://dotnet.microsoft.com/)
[![Playwright](https://img.shields.io/badge/Playwright-Extra-2EAD33?logo=playwright)](https://playwright.dev/)

A powerful web scraper that extracts product pricing and information from the **Pak'n'Save NZ** website. Logs data to console and optionally stores price history in Azure CosmosDB.

## ✨ Features
- 🎭 **Headless Browser** — Powered by **Playwright** & **PlaywrightExtraSharp2**
- 📊 **Price Tracking** — Store historical price data to track changes over time
- 🖼️ **Image Processing** — Send product images to a REST API for resizing and analysis
- 📍 **Location-Aware** — Configure store location using geolocation coordinates

## 🚀 Quick Start

### Prerequisites
- [.NET SDK 8.0 or newer](https://dotnet.microsoft.com/download)

### Installation
```bash
# Clone the repository
git clone https://github.com/Jason-nzd/pakn-scraper
cd src

# Restore and build dependencies
dotnet restore
dotnet build

# Run the scraper (dry run mode)
dotnet run
```

> 💡The first run will automatically install Playwright browser runtimes.

### 📋 Sample Console Output
```cmd
       ID | Name                              | Size   | Price  | Unit Price
----------------------------------------------------------------------------
 P1234567 | Coconut Supreme Slice             | 350g   | $ 5.89 | $16.83 /kg
 P5345284 | Gluten Free Choc Chip Cookie      | 250g   | $ 4.89 | $19.56 /kg
 P5678287 | Gluten Free Macadamia Cookie      | 250g   | $ 4.89 | $19.56 /kg
 P3457825 | Belgium Slice                     | Each   | $ 5.89 |
 P5789285 | Gluten Free Double Choc Chip      | 250g   | $ 4.89 | $19.56 /kg
 P2356288 | Bakery Crunchy Bran               | 230g   | $ 4.49 | $19.52 /kg
 P2765307 | Sanniu Evergreen Variant Biscuits | 4x132g | $ 6.36 | $12.05 /kg
```

---

## ⚙️ appsettings.json

> Optional advanced parameters can be configured in `appsettings.json`:

Store product information and price snapshots in CosmosDB by defining:

```json
{
  "COSMOS_ENDPOINT": "<your-cosmosdb-endpoint-uri>",
  "COSMOS_KEY": "<your-cosmosdb-primary-key>",
  "COSMOS_DB": "<database-name>",
  "COSMOS_CONTAINER": "<container-name>"
}
```
Override the default store location by setting geolocation coordinates (Longitude/Latitude):
```json
{
  "GEOLOCATION_LONG": "174.91",
  "GEOLOCATION_LAT": "-41.21"
}
```
> 💡 Coordinates can be obtained from Google Maps or similar services. The website will geolocate to the closest store.

Send scraped product images to a REST API for processing:
```json
{
  "IMAGE_PROCESS_API_URL": "<your-rest-api-endpoint>"
}
```

### 💻 Command-Line Usage
- ```dotnet run``` - Dry run - logs products to console only
- ```dotnet run db``` - Stores scraped data into CosmosDB
- ```dotnet run db images``` - Store to DB + send images to API
- ```dotnet run headed``` - Run with visible browser for debugging

### 📄 Sample Cosmos DB Document
Products stored in Cosmos DB include price history for trend analysis:

```json
{
  "id": "P1234567",
  "name": "Supreme Lite Milk",
  "size": "1L",
  "category": "milk",
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
      "price": 3.40
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