using Kamus.Inventory.Contracts;
using Kamus.Inventory.Domain;
using Kamus.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Inventory.Application;

internal sealed class InventoryService(InventoryDbContext db) : IInventoryService
{
    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailabilityAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default)
    {
        var levels = await db.StockLevels
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.SkuId))
            .ToDictionaryAsync(s => s.SkuId, s => s.Quantity, cancellationToken);

        // SKU sem registro de estoque = indisponível
        return skuIds.Distinct().ToDictionary(id => id, id => levels.GetValueOrDefault(id));
    }

    public async Task SetStockAsync(IReadOnlyDictionary<Guid, int> quantities, CancellationToken cancellationToken = default)
    {
        var ids = quantities.Keys.ToList();
        var existing = await db.StockLevels
            .Where(s => ids.Contains(s.SkuId))
            .ToDictionaryAsync(s => s.SkuId, cancellationToken);

        foreach (var (skuId, quantity) in quantities)
        {
            if (existing.TryGetValue(skuId, out var level))
            {
                level.SetQuantity(quantity);
            }
            else
            {
                db.StockLevels.Add(new StockLevel(skuId, quantity));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
