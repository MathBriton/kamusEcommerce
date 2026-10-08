using Kamus.Catalog.Api;
using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.Inventory.Contracts;
using Kamus.Shared.Pagination;
using Kamus.Shared.Results;
using Kamus.Shared.Storage;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Catalog.Application;

internal sealed class CatalogQueries(CatalogDbContext db, IInventoryService inventory, IFileStorage storage)
{
    public async Task<IReadOnlyList<CategoryNode>> GetCategoryTreeAsync(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        var byParent = categories.ToLookup(c => c.ParentId);

        IReadOnlyList<CategoryNode> Build(Guid? parentId) =>
            [.. byParent[parentId].Select(c => new CategoryNode(c.Id, c.Name, c.Slug, c.Path, Build(c.Id)))];

        return Build(null);
    }

    public async Task<IReadOnlyList<CollectionDto>> GetCollectionsAsync(CancellationToken ct) =>
        await db.Collections.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CollectionDto(c.Name, c.Slug, c.Description))
            .ToListAsync(ct);

    public async Task<Result<ProductListPage>> ListProductsAsync(ListProductsRequest request, CancellationToken ct)
    {
        var sort = request.ParsedSort;
        ProductCursor? cursor = null;
        if (request.Cursor is not null && (!Cursor.TryDecode(request.Cursor, out cursor) || cursor!.Sort != sort))
        {
            return Error.Validation("catalog.invalid_cursor", "Cursor inválido para esta ordenação.");
        }

        var rows = ApplyFilters(db.Products.AsNoTracking(), request)
            .Select(p => new ProductRow
            {
                Id = p.Id,
                Name = p.Name,
                CreatedAt = p.CreatedAt,
                MinPrice = p.Skus.Min(s => s.SalePrice ?? s.Price),
            });

        if (request.MinPrice is { } min)
        {
            rows = rows.Where(r => r.MinPrice >= min);
        }

        if (request.MaxPrice is { } max)
        {
            rows = rows.Where(r => r.MinPrice <= max);
        }

        if (cursor is not null)
        {
            rows = ApplyKeyset(rows, cursor);
        }

        var limit = request.EffectiveLimit;
        var page = await ApplyOrder(rows, sort).Take(limit + 1).ToListAsync(ct);

        var hasMore = page.Count > limit;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        var cards = await LoadCardsAsync(page.Select(r => r.Id).ToList(), ct);
        var items = page.Select(r => cards[r.Id]).ToList();
        var next = hasMore ? Cursor.Encode(ProductCursor.From(sort, page[^1])) : null;

        return new ProductListPage(items, next);
    }

    /// <summary>Facetas do escopo (categoria/coleção), sem aplicar os demais filtros.</summary>
    public async Task<CatalogFacets> GetFacetsAsync(string? category, string? collection, CancellationToken ct)
    {
        var scope = ApplyFilters(db.Products.AsNoTracking(), new ListProductsRequest(category, collection, null, null, null, null, null, null, null));
        var skus = db.Skus.AsNoTracking().Where(s => scope.Any(p => p.Id == s.ProductId));

        var total = await scope.CountAsync(ct);

        var sizes = await skus
            .GroupBy(s => s.Size)
            .Select(g => new { Size = g.Key, Order = g.Min(s => s.SizeOrder) })
            .OrderBy(s => s.Order).ThenBy(s => s.Size)
            .Select(s => s.Size)
            .ToListAsync(ct);

        var colors = await skus
            .GroupBy(s => new { s.Color, s.ColorHex })
            .OrderBy(g => g.Key.Color)
            .Select(g => new ColorDto(g.Key.Color, g.Key.ColorHex))
            .ToListAsync(ct);

        var prices = await skus
            .GroupBy(_ => 1)
            .Select(g => new { Min = g.Min(s => s.SalePrice ?? s.Price), Max = g.Max(s => s.SalePrice ?? s.Price) })
            .FirstOrDefaultAsync(ct);

        return new CatalogFacets(total, sizes, colors, prices?.Min, prices?.Max);
    }

    public async Task<Result<ProductDetail>> GetProductAsync(string slug, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Collection)
            .Include(p => p.Skus)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive, ct);

        if (product is null)
        {
            return Error.NotFound("catalog.product_not_found", $"Produto '{slug}' não encontrado.");
        }

        var availability = await inventory.GetAvailabilityAsync([.. product.Skus.Select(s => s.Id)], ct);
        var breadcrumb = await GetBreadcrumbAsync(product.Category, ct);
        var cheapest = Cheapest(product.Skus);

        // Ordem explícita das cores (o banco não garante a ordem dos SKUs): a mesma do desempate da
        // foto do card na listagem, para a PDP abrir na cor que o cliente acabou de ver.
        var colors = product.Skus
            .GroupBy(s => new { s.Color, s.ColorHex })
            .OrderBy(g => g.Key.Color, StringComparer.Ordinal)
            .Select(g => new ColorOption(
                g.Key.Color,
                g.Key.ColorHex,
                [.. product.Images.Where(i => i.Color == g.Key.Color).OrderBy(i => i.SortOrder).Select(ToImage)],
                [.. g.OrderBy(s => s.SizeOrder).Select(s => new SizeOption(s.Id, s.Code, s.Size, s.Price, s.SalePrice, availability.GetValueOrDefault(s.Id)))]))
            .ToList();

        return new ProductDetail(
            product.Id,
            product.Slug,
            product.Name,
            product.Brand,
            product.Description,
            $"{product.Category.Path}/{product.Slug}",
            breadcrumb,
            product.Collection is { } c ? new CollectionDto(c.Name, c.Slug, c.Description) : null,
            cheapest.Price,
            cheapest.SalePrice,
            colors,
            product.UpdatedAt);
    }

    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapAsync(CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new SitemapEntry(p.Category.Path + "/" + p.Slug, p.UpdatedAt))
            .ToListAsync(ct);

        var lastUpdate = products.Count > 0 ? products.Max(p => p.UpdatedAt) : DateTimeOffset.UtcNow;
        var categories = await db.Categories.AsNoTracking().Select(c => c.Path).ToListAsync(ct);

        return [.. categories.Select(path => new SitemapEntry(path, lastUpdate)), .. products];
    }

    private static IQueryable<Product> ApplyFilters(IQueryable<Product> products, ListProductsRequest request)
    {
        products = products.Where(p => p.IsActive && p.Skus.Any());

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            // Inclui as subcategorias: "masculino" traz "masculino/calcas/jeans".
            var path = request.Category.Trim('/').ToLowerInvariant();
            var prefix = path + "/";
            products = products.Where(p => p.Category.Path == path || p.Category.Path.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(request.Collection))
        {
            products = products.Where(p => p.Collection!.Slug == request.Collection);
        }

        if (request.Size is { Length: > 0 } sizes && request.Color is { Length: > 0 } colors)
        {
            // Cor e tamanho juntos precisam existir no MESMO SKU (ex.: "Azul" no tamanho "M").
            products = products.Where(p => p.Skus.Any(s => sizes.Contains(s.Size) && colors.Contains(s.Color)));
        }
        else if (request.Size is { Length: > 0 } onlySizes)
        {
            products = products.Where(p => p.Skus.Any(s => onlySizes.Contains(s.Size)));
        }
        else if (request.Color is { Length: > 0 } onlyColors)
        {
            products = products.Where(p => p.Skus.Any(s => onlyColors.Contains(s.Color)));
        }

        return products;
    }

    private static IQueryable<ProductRow> ApplyOrder(IQueryable<ProductRow> rows, ProductSort sort) => sort switch
    {
        ProductSort.PriceAsc => rows.OrderBy(r => r.MinPrice).ThenBy(r => r.Id),
        ProductSort.PriceDesc => rows.OrderByDescending(r => r.MinPrice).ThenByDescending(r => r.Id),
        ProductSort.Name => rows.OrderBy(r => r.Name).ThenBy(r => r.Id),
        _ => rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
    };

    /// <summary>Paginação keyset: continua estritamente depois do último item (chave de ordenação + id).</summary>
    private static IQueryable<ProductRow> ApplyKeyset(IQueryable<ProductRow> rows, ProductCursor c) => c.Sort switch
    {
        ProductSort.PriceAsc => rows.Where(r => r.MinPrice > c.Price || (r.MinPrice == c.Price && r.Id.CompareTo(c.Id) > 0)),
        ProductSort.PriceDesc => rows.Where(r => r.MinPrice < c.Price || (r.MinPrice == c.Price && r.Id.CompareTo(c.Id) < 0)),
        ProductSort.Name => rows.Where(r => r.Name.CompareTo(c.Name) > 0 || (r.Name == c.Name && r.Id.CompareTo(c.Id) > 0)),
        _ => rows.Where(r => r.CreatedAt < c.CreatedAt || (r.CreatedAt == c.CreatedAt && r.Id.CompareTo(c.Id) < 0)),
    };

    private async Task<Dictionary<Guid, ProductCard>> LoadCardsAsync(List<Guid> ids, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Name,
                p.Brand,
                CategoryPath = p.Category.Path,
                Skus = p.Skus.Select(s => new { s.Color, s.ColorHex, s.Price, s.SalePrice }).ToList(),
                Image = p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Color).Select(i => new { i.StorageKey, i.Alt }).FirstOrDefault(),
            })
            .AsSplitQuery()
            .ToListAsync(ct);

        return products.ToDictionary(p => p.Id, p =>
        {
            var cheapest = p.Skus.OrderBy(s => s.SalePrice ?? s.Price).First();
            return new ProductCard(
                p.Id,
                p.Slug,
                p.Name,
                p.Brand,
                $"{p.CategoryPath}/{p.Slug}",
                cheapest.Price,
                cheapest.SalePrice,
                p.Image is { } i ? new ImageDto(storage.GetPublicUrl(i.StorageKey), i.Alt) : null,
                [.. p.Skus.DistinctBy(s => s.Color).OrderBy(s => s.Color, StringComparer.Ordinal).Select(s => new ColorDto(s.Color, s.ColorHex))]);
        });
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> GetBreadcrumbAsync(Category category, CancellationToken ct)
    {
        var segments = category.Path.Split('/');
        var paths = Enumerable.Range(1, segments.Length).Select(n => string.Join('/', segments[..n])).ToList();
        var names = await db.Categories.AsNoTracking()
            .Where(c => paths.Contains(c.Path))
            .ToDictionaryAsync(c => c.Path, c => c.Name, ct);

        return [.. paths.Select(p => new BreadcrumbItem(names[p], p))];
    }

    private ImageDto ToImage(ProductImage image) => new(storage.GetPublicUrl(image.StorageKey), image.Alt);

    private static Sku Cheapest(IEnumerable<Sku> skus) => skus.OrderBy(s => s.SalePrice ?? s.Price).First();
}

internal sealed class ProductRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = null!;

    public DateTimeOffset CreatedAt { get; init; }

    public decimal MinPrice { get; init; }
}

internal sealed record ProductCursor(ProductSort Sort, Guid Id, decimal? Price, DateTimeOffset? CreatedAt, string? Name)
{
    public static ProductCursor From(ProductSort sort, ProductRow last) => sort switch
    {
        ProductSort.PriceAsc or ProductSort.PriceDesc => new(sort, last.Id, last.MinPrice, null, null),
        ProductSort.Name => new(sort, last.Id, null, null, last.Name),
        _ => new(sort, last.Id, null, last.CreatedAt, null),
    };
}
