namespace Kamus.Inventory.Contracts;

/// <summary>Contrato público do módulo Inventory.</summary>
public interface IInventoryService
{
    /// <summary>Quantidade disponível para venda (estoque físico menos reservas ativas) por SKU.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAvailabilityAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default);

    /// <summary>Define o estoque físico de SKUs (usado por seed e, futuramente, pelo painel administrativo).</summary>
    Task SetStockAsync(IReadOnlyDictionary<Guid, int> quantities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserva estoque para um pedido por tempo limitado. Tudo ou nada: se faltar qualquer SKU,
    /// nada é reservado e as faltas são devolvidas.
    /// </summary>
    Task<ReservationResult> ReserveAsync(Guid orderId, IReadOnlyCollection<ReservationLine> lines, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma a reserva (pagamento aprovado): baixa o estoque físico. Idempotente.
    /// Retorna <c>false</c> se a reserva não está mais ativa (expirou ou foi liberada).
    /// </summary>
    Task<bool> CommitReservationAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Libera a reserva (pagamento recusado ou pedido cancelado). Idempotente.</summary>
    Task ReleaseReservationAsync(Guid orderId, CancellationToken cancellationToken = default);
}

public sealed record ReservationLine(Guid SkuId, int Quantity);

public sealed record StockShortage(Guid SkuId, int Requested, int Available);

public sealed record ReservationResult(bool Succeeded, DateTimeOffset? ExpiresAt, IReadOnlyList<StockShortage> Shortages)
{
    public static ReservationResult Reserved(DateTimeOffset expiresAt) => new(true, expiresAt, []);

    public static ReservationResult Failed(IReadOnlyList<StockShortage> shortages) => new(false, null, shortages);
}

/// <summary>Evento: uma reserva venceu sem confirmação e o estoque voltou a ficar disponível.</summary>
public sealed record ReservationExpired(Guid OrderId);
