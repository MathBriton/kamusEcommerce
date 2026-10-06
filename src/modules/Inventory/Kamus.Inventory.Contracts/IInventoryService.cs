namespace Kamus.Inventory.Contracts;

/// <summary>Contrato público do módulo Inventory.</summary>
public interface IInventoryService
{
    /// <summary>Quantidade disponível para venda (estoque físico menos reservas ativas) por SKU.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAvailabilityAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default);

    /// <summary>Define o estoque físico de SKUs (usado por seed e, futuramente, pelo painel administrativo).</summary>
    Task SetStockAsync(IReadOnlyDictionary<Guid, int> quantities, CancellationToken cancellationToken = default);
}
