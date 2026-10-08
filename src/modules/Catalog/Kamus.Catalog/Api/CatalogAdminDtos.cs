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

/// <summary>Quem mandou o item para a lixeira (id nulo quando foi uma ação automática).</summary>
public sealed record TrashActorDto(Guid? Id, string? Name);

/// <summary>
/// Item da lixeira. <c>Type</c>: "product", "sku" ou "image". SKUs e imagens excluídos junto com o
/// produto não aparecem separados: entram em <c>SkuCount</c>/<c>ImageCount</c> do produto.
/// </summary>
/// <param name="Name">Produto: nome; SKU: "Produto · Cor · Tamanho"; imagem: "Produto · foto N".</param>
/// <param name="Detail">Produto: "Marca · N SKUs · M imagens"; SKU: código; imagem: "Cor X".</param>
/// <param name="ImageUrl">Produto: primeira imagem; SKU: primeira imagem da cor; imagem: ela mesma.</param>
/// <param name="PurgeAt">Quando o expurgo automático apaga o item de vez.</param>
/// <param name="WasActive">Produto: estava publicado. SKU/imagem: o produto está publicado.</param>
public sealed record TrashItemDto(
    string Type,
    Guid Id,
    Guid ProductId,
    string Name,
    string? Detail,
    string? ImageUrl,
    DateTimeOffset DeletedAt,
    TrashActorDto? DeletedBy,
    DateTimeOffset PurgeAt,
    int SkuCount,
    int ImageCount,
    bool WasActive);

/// <summary>Quantidade de itens na lixeira por tipo (não depende do filtro).</summary>
public sealed record TrashCounts(int Product, int Sku, int Image);

/// <summary>Página da lixeira. <c>Total</c> conta os itens do tipo filtrado (todos, em "all").</summary>
public sealed record TrashPage(IReadOnlyList<TrashItemDto> Items, int Total, TrashCounts Counts);

/// <summary>Resultado da restauração: frase pronta para exibir ao usuário.</summary>
public sealed record RestoreResult(string Message);
