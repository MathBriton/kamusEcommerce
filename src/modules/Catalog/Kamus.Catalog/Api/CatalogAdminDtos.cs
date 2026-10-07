namespace Kamus.Catalog.Api;

public sealed record AdminProductRow(
    Guid Id,
    string Name,
    string Slug,
    string Path,
    string Brand,
    string CategoryName,
    bool IsActive,
    int SkuCount,
    decimal? MinPrice,
    int Available,
    string? ImageUrl,
    DateTimeOffset UpdatedAt);

public sealed record AdminPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record AdminSku(
    Guid Id,
    string Code,
    string Color,
    string ColorHex,
    string Size,
    decimal Price,
    decimal? SalePrice,
    int Quantity,
    int Reserved,
    int Available);

public sealed record AdminImage(Guid Id, string Color, string Url, string Alt, int SortOrder);

public sealed record AdminProductDetail(
    Guid Id,
    string Name,
    string Slug,
    string Path,
    string Description,
    string Brand,
    Guid CategoryId,
    Guid? CollectionId,
    bool IsActive,
    IReadOnlyList<AdminSku> Skus,
    IReadOnlyList<AdminImage> Images,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminCollection(Guid Id, string Name, string Slug);

public sealed record SaveProductRequest(string Name, string Description, string Brand, Guid CategoryId, Guid? CollectionId);

/// <summary>Cria um SKU para cada tamanho informado, todos na mesma cor e preço.</summary>
public sealed record AddSkusRequest(string Color, string ColorHex, string[] Sizes, decimal Price, decimal? SalePrice, int InitialStock);

public sealed record UpdateSkuPricesRequest(decimal Price, decimal? SalePrice);
