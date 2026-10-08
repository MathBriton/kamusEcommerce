using Kamus.Catalog.Contracts;
using Kamus.Shared.Events;
using Microsoft.Extensions.Logging;

namespace Kamus.Inventory.Application;

/// <summary>Reage a eventos de outros módulos que afetam o estoque.</summary>
internal sealed class InventoryEventHandlers(InventoryService inventory, ILogger<InventoryEventHandlers> logger)
    : IEventHandler<SkusPurged>
{
    /// <summary>
    /// SKUs apagados de vez do catálogo: o estoque deles some também (fica na auditoria como
    /// "purged"). Idempotente: SKU sem estoque registrado é ignorado.
    /// </summary>
    public async Task HandleAsync(SkusPurged @event, CancellationToken ct)
    {
        var removed = await inventory.RemoveStockAsync(@event.SkuIds, ct);
        if (removed > 0)
        {
            logger.LogInformation("Estoque de {Count} SKU(s) expurgado(s) do catálogo removido", removed);
        }
    }
}
