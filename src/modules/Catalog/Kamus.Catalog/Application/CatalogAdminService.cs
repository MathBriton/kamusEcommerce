using FluentValidation;
using Kamus.Catalog.Api;
using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.Inventory.Contracts;
using Kamus.Shared.Results;
using Kamus.Shared.Storage;
using Kamus.Shared.Text;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Catalog.Application;

/// <summary>Casos de uso do backoffice para o catálogo.</summary>
internal sealed class CatalogAdminService(
    CatalogDbContext db,
    IInventoryService inventory,
    IFileStorage storage,
    TimeProvider clock)
{
    public const int MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>Ordem de exibição de tamanhos conhecidos; os demais vão para o fim.</summary>
    private static readonly string[] SizeOrder = ["PP", "P", "M", "G", "GG", "XG", "34", "36", "38", "40", "42", "44", "46", "48", "U"];

    public async Task<AdminPage<AdminProductRow>> ListAsync(string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, term) || EF.Functions.ILike(p.Slug, term)
                || p.Skus.Any(s => EF.Functions.ILike(s.Code, term)));
        }

        query = status switch
        {
            "active" => query.Where(p => p.IsActive),
            "inactive" => query.Where(p => !p.IsActive),
            _ => query,
        };

        // Paginação por número de página: no backoffice, saltar páginas e ver o total importa
        // mais que o custo do OFFSET (poucos milhares de linhas). Ver ADR 0005 para a vitrine.
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                Path = p.Category.Path + "/" + p.Slug,
                p.Brand,
                CategoryName = p.Category.Name,
                p.IsActive,
                SkuIds = p.Skus.Select(s => s.Id).ToList(),
                MinPrice = p.Skus.Min(s => (decimal?)(s.SalePrice ?? s.Price)),
                Image = p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Color).Select(i => i.StorageKey).FirstOrDefault(),
                p.UpdatedAt,
            })
            .ToListAsync(ct);

        var levels = await inventory.GetStockLevelsAsync([.. rows.SelectMany(r => r.SkuIds)], ct);

        return new AdminPage<AdminProductRow>(
            [.. rows.Select(r => new AdminProductRow(
                r.Id, r.Name, r.Slug, r.Path, r.Brand, r.CategoryName, r.IsActive, r.SkuIds.Count, r.MinPrice,
                r.SkuIds.Sum(id => levels[id].Available),
                r.Image is null ? null : storage.GetPublicUrl(r.Image),
                r.UpdatedAt))],
            total,
            page,
            pageSize);
    }

    public async Task<IReadOnlyList<AdminCollection>> CollectionsAsync(CancellationToken ct) =>
        await db.Collections.AsNoTracking().OrderBy(c => c.Name).Select(c => new AdminCollection(c.Id, c.Name, c.Slug)).ToListAsync(ct);

    public async Task<Result<AdminProductDetail>> GetAsync(Guid id, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        return product is null ? NotFound() : await ToDetailAsync(product, ct);
    }

    public async Task<Result<AdminProductDetail>> CreateAsync(SaveProductRequest request, CancellationToken ct)
    {
        if (await ValidateReferencesAsync(request, ct) is { } error)
        {
            return error;
        }

        var product = Product.CreateDraft(
            request.Name.Trim(),
            await UniqueSlugAsync(request.Name, ct),
            request.Description.Trim(),
            request.Brand.Trim(),
            request.CategoryId,
            request.CollectionId,
            clock.GetUtcNow());

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return await GetAsync(product.Id, ct);
    }

    public async Task<Result<AdminProductDetail>> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        if (await ValidateReferencesAsync(request, ct) is { } error)
        {
            return error;
        }

        product.Update(request.Name.Trim(), request.Description.Trim(), request.Brand.Trim(), request.CategoryId, request.CollectionId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(product, ct);
    }

    public async Task<Result<AdminProductDetail>> SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        if (active && !product.Activate(clock.GetUtcNow()))
        {
            return Error.Conflict("catalog.product_without_skus", "Cadastre ao menos um SKU antes de publicar o produto.");
        }

        if (!active)
        {
            product.Deactivate(clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(product, ct);
    }

    public async Task<Result<AdminProductDetail>> AddSkusAsync(Guid id, AddSkusRequest request, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        var color = new ColorInfo(request.Color.Trim(), request.ColorHex.ToLowerInvariant());
        var sizes = request.Sizes.Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();
        var duplicated = sizes.Where(size => product.Skus.Any(s => s.Color == color.Name && s.Size == size)).ToList();
        if (duplicated.Count > 0)
        {
            return Error.Conflict("catalog.sku_exists", $"Já existe {color.Name} nos tamanhos {string.Join(", ", duplicated)}.");
        }

        var created = new List<Sku>();
        foreach (var size in sizes)
        {
            var order = Array.IndexOf(SizeOrder, size) is var index and >= 0 ? index : SizeOrder.Length;
            created.Add(product.AddSku(SkuCode(product, color.Name, size), color, size, order, request.Price, request.SalePrice));
        }

        await db.SaveChangesAsync(ct);
        await inventory.SetStockAsync(created.ToDictionary(s => s.Id, _ => request.InitialStock), ct);
        return await ToDetailAsync(product, ct);
    }

    public async Task<Result<AdminProductDetail>> UpdateSkuPricesAsync(Guid skuId, UpdateSkuPricesRequest request, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Skus).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Skus.Any(s => s.Id == skuId), ct);
        if (product is null)
        {
            return SkuNotFound();
        }

        product.Skus.Single(s => s.Id == skuId).UpdatePrices(request.Price, request.SalePrice);
        product.Update(product.Name, product.Description, product.Brand, product.CategoryId, product.CollectionId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(product, ct);
    }

    public async Task<Result<AdminProductDetail>> AddImageAsync(Guid id, string color, Stream content, long length, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        if (product.Skus.All(s => s.Color != color))
        {
            return Error.Validation("catalog.unknown_color", "Cadastre um SKU nessa cor antes de enviar a imagem.");
        }

        if (length is <= 0 or > MaxImageBytes)
        {
            return Error.Validation("catalog.image_size", "A imagem precisa ter até 5 MB.");
        }

        // O tipo é decidido pelos primeiros bytes do arquivo, não pela extensão nem pelo
        // Content-Type enviados pelo navegador (que podem ser forjados). SVG não é aceito.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var extension = ImageSignature.Detect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        if (extension is null)
        {
            return Error.Validation("catalog.image_type", "Envie uma imagem JPEG, PNG ou WebP.");
        }

        var key = $"products/{product.Slug}/{Guid.NewGuid():N}.{extension}";
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, $"image/{extension}", ct);

        product.AddImage(color, key, $"{product.Name} {color.ToLowerInvariant()}");
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(product, ct);
    }

    /// <summary>A imagem vai para a lixeira (o arquivo só é apagado no expurgo).</summary>
    public async Task<Result<AdminProductDetail>> RemoveImageAsync(Guid id, Guid imageId, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        if (product.RemoveImage(imageId, clock.GetUtcNow()) is not { } image)
        {
            return Error.NotFound("catalog.image_not_found", "Imagem não encontrada.");
        }

        db.Images.Remove(image);
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(product, ct);
    }

    /// <summary>
    /// Manda o produto para a lixeira com todos os SKUs e imagens ativos (mesmo instante, para que
    /// voltem juntos). O estoque fica como está: se o produto for restaurado, volta com ele.
    /// </summary>
    public async Task<Result> DeleteProductAsync(Guid id, CancellationToken ct)
    {
        var product = await db.Products.Include(p => p.Skus).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return NotFound();
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Manda um SKU para a lixeira. Produto publicado não pode ficar sem variação: a última precisa
    /// esperar a despublicação.
    /// </summary>
    public async Task<Result<AdminProductDetail>> DeleteSkuAsync(Guid skuId, CancellationToken ct)
    {
        var product = await db.Products.Include(p => p.Skus).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Skus.Any(s => s.Id == skuId), ct);
        if (product is null)
        {
            return SkuNotFound();
        }

        if (product.IsLastSkuOnSale(skuId))
        {
            return Error.Conflict("catalog.last_sku", "Despublique o produto antes de excluir a última variação.");
        }

        if (product.RemoveSku(skuId, clock.GetUtcNow()) is not { } sku)
        {
            return SkuNotFound();
        }

        db.Skus.Remove(sku);
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(product, ct);
    }

    /// <summary>
    /// Código curto e legível para etiqueta e planilha: iniciais do produto + 4 caracteres do id,
    /// cor abreviada e tamanho. Ex.: "Camisa de Linho Terracota" → <c>CDLT3F9A-AREI-M</c>.
    /// </summary>
    internal static string SkuCode(Product product, string color, string size)
    {
        var initials = string.Concat(product.Slug.Split('-', StringSplitOptions.RemoveEmptyEntries).Take(5).Select(w => w[0]));
        var suffix = product.Id.ToString("N")[^4..];
        var colorCode = Slug.From(color).Replace("-", string.Empty, StringComparison.Ordinal);
        return $"{initials}{suffix}-{colorCode[..Math.Min(4, colorCode.Length)]}-{size}".ToUpperInvariant();
    }

    private Task<Product?> LoadAsync(Guid id, CancellationToken ct) =>
        db.Products.Include(p => p.Category).Include(p => p.Skus).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    private async Task<AdminProductDetail> ToDetailAsync(Product product, CancellationToken ct)
    {
        var levels = await inventory.GetStockLevelsAsync([.. product.Skus.Select(s => s.Id)], ct);
        var categoryPath = await db.Categories.Where(c => c.Id == product.CategoryId).Select(c => c.Path).SingleAsync(ct);

        return new AdminProductDetail(
            product.Id,
            product.Name,
            product.Slug,
            $"{categoryPath}/{product.Slug}",
            product.Description,
            product.Brand,
            product.CategoryId,
            product.CollectionId,
            product.IsActive,
            [.. product.Skus.OrderBy(s => s.Color).ThenBy(s => s.SizeOrder).Select(s =>
            {
                var level = levels[s.Id];
                return new AdminSku(s.Id, s.Code, s.Color, s.ColorHex, s.Size, s.Price, s.SalePrice, level.Quantity, level.Reserved, level.Available);
            })],
            [.. product.Images.OrderBy(i => i.Color).ThenBy(i => i.SortOrder)
                .Select(i => new AdminImage(i.Id, i.Color, storage.GetPublicUrl(i.StorageKey), i.Alt, i.SortOrder))],
            product.CreatedAt,
            product.UpdatedAt);
    }

    private async Task<Error?> ValidateReferencesAsync(SaveProductRequest request, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            return Error.Validation("catalog.unknown_category", "Categoria inexistente.");
        }

        if (request.CollectionId is { } collectionId && !await db.Collections.AnyAsync(c => c.Id == collectionId, ct))
        {
            return Error.Validation("catalog.unknown_collection", "Coleção inexistente.");
        }

        return null;
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slug.From(name);
        var slug = baseSlug;
        for (var i = 2; await db.Products.AnyAsync(p => p.Slug == slug, ct); i++)
        {
            slug = $"{baseSlug}-{i}";
        }

        return slug;
    }

    private static Error NotFound() => Error.NotFound("catalog.product_not_found", "Produto não encontrado.");

    private static Error SkuNotFound() => Error.NotFound("catalog.sku_not_found", "SKU não encontrado.");
}

/// <summary>Identifica o formato da imagem pelos "magic bytes".</summary>
internal static class ImageSignature
{
    public static string? Detect(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "jpeg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "png",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "webp",
        _ => null,
    };
}

internal sealed class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).NotEmpty().MaximumLength(4000);
        RuleFor(r => r.Brand).NotEmpty().MaximumLength(100);
        RuleFor(r => r.CategoryId).NotEmpty();
    }
}

internal sealed class AddSkusRequestValidator : AbstractValidator<AddSkusRequest>
{
    public AddSkusRequestValidator()
    {
        RuleFor(r => r.Color).NotEmpty().MaximumLength(50);
        RuleFor(r => r.ColorHex).Matches("^#[0-9a-fA-F]{6}$").WithMessage("Cor em hexadecimal, ex.: #1f1f1f.");
        RuleFor(r => r.Sizes).NotEmpty().Must(s => s.Length <= 15);
        RuleForEach(r => r.Sizes).NotEmpty().MaximumLength(10);
        RuleFor(r => r.Price).GreaterThan(0);
        RuleFor(r => r.SalePrice).GreaterThan(0).LessThan(r => r.Price)
            .When(r => r.SalePrice is not null)
            .WithMessage("O preço promocional precisa ser menor que o preço cheio.");
        RuleFor(r => r.InitialStock).InclusiveBetween(0, 100_000);
    }
}

internal sealed class UpdateSkuPricesRequestValidator : AbstractValidator<UpdateSkuPricesRequest>
{
    public UpdateSkuPricesRequestValidator()
    {
        RuleFor(r => r.Price).GreaterThan(0);
        RuleFor(r => r.SalePrice).GreaterThan(0).LessThan(r => r.Price)
            .When(r => r.SalePrice is not null)
            .WithMessage("O preço promocional precisa ser menor que o preço cheio.");
    }
}
