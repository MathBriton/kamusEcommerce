using Kamus.Catalog.Contracts;
using Kamus.Catalog.Persistence;
using Kamus.Shared.Storage;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Catalog.Application;

internal sealed class CatalogService(CatalogDbContext db, IFileStorage storage) : ICatalogService
{
    public async Task<IReadOnlyDictionary<Guid, SkuSnapshot>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default)
    {
        var rows = await db.Skus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .Join(db.Products.Where(p => p.IsActive), s => s.ProductId, p => p.Id, (s, p) => new
            {
                Sku = s,
                p.Name,
                p.Slug,
                CategoryPath = p.Category.Path,
                Image = p.Images
                    .Where(i => i.Color == s.Color)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.StorageKey)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Sku.Id, r => new SkuSnapshot(
            r.Sku.Id,
            r.Sku.Code,
            r.Sku.ProductId,
            r.Name,
            $"{r.CategoryPath}/{r.Slug}",
            r.Sku.Color,
            r.Sku.Size,
            r.Sku.Price,
            r.Sku.SalePrice,
            r.Image is null ? null : storage.GetPublicUrl(r.Image)));
    }
}
