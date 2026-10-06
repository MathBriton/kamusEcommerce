namespace Kamus.Catalog.Api;

public sealed record CategoryNode(Guid Id, string Name, string Slug, string Path, IReadOnlyList<CategoryNode> Children);

public sealed record CollectionDto(string Name, string Slug, string Description);

public sealed record ImageDto(string Url, string Alt);

public sealed record ColorDto(string Name, string Hex);

/// <summary>Card de produto na PLP. Preço "de/por" do SKU mais barato.</summary>
public sealed record ProductCard(
    Guid Id,
    string Slug,
    string Name,
    string Brand,
    string Path,
    decimal Price,
    decimal? SalePrice,
    ImageDto? Image,
    IReadOnlyList<ColorDto> Colors);

public sealed record ProductListPage(IReadOnlyList<ProductCard> Items, string? NextCursor);

public sealed record CatalogFacets(
    int Total,
    IReadOnlyList<string> Sizes,
    IReadOnlyList<ColorDto> Colors,
    decimal? MinPrice,
    decimal? MaxPrice);

public sealed record BreadcrumbItem(string Name, string Path);

public sealed record SizeOption(Guid SkuId, string Code, string Size, decimal Price, decimal? SalePrice, int Available);

public sealed record ColorOption(string Name, string Hex, IReadOnlyList<ImageDto> Images, IReadOnlyList<SizeOption> Sizes);

public sealed record ProductDetail(
    Guid Id,
    string Slug,
    string Name,
    string Brand,
    string Description,
    string Path,
    IReadOnlyList<BreadcrumbItem> Breadcrumb,
    CollectionDto? Collection,
    decimal Price,
    decimal? SalePrice,
    IReadOnlyList<ColorOption> Colors,
    DateTimeOffset UpdatedAt);

public sealed record SitemapEntry(string Path, DateTimeOffset UpdatedAt);
