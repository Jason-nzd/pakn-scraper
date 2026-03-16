public record Product(
    string id,
    string name,
    string? size,
    string category,
    string sourceSite,
    float currentPrice,
    string unitPrice
);

public record DBProduct(
    string id,
    string name,
    string? size,
    string category,
    string sourceSite,
    DatedPrice[] priceHistory,
    string lastUpdated,
    string unitPrice
);

public record DatedPrice(
    string date,
    float price
);
public enum UpsertResponse
{
    NewProduct,
    PriceUpdated,
    NonPriceUpdated,
    AlreadyUpToDate,
    Failed
}

public struct ProductResponse
{
    public UpsertResponse upsertResponse;
    public DBProduct dbProduct;

    public ProductResponse(UpsertResponse upsertResponse, DBProduct dbProduct) : this()
    {
        this.upsertResponse = upsertResponse;
        this.dbProduct = dbProduct;
    }
}