namespace Kamus.Orders.Contracts;

/// <summary>
/// Consultas agregadas sobre pedidos para o módulo Reporting. O Orders continua dono dos próprios
/// dados: quem quer números pergunta pelo contrato, nunca lê as tabelas (ADR 0012).
/// </summary>
public interface IOrderReports
{
    /// <summary>Pedidos criados no intervalo [from, to), apenas os campos usados em agregações.</summary>
    Task<IReadOnlyList<OrderFact>> GetOrderFactsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Itens vendidos (pedidos pagos, enviados ou entregues) no intervalo, agrupados por SKU.</summary>
    Task<IReadOnlyList<SkuSales>> GetTopSkusAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken = default);
}

public sealed record OrderFact(Guid OrderId, DateTimeOffset CreatedAt, string Status, decimal Total, int Units);

public sealed record SkuSales(Guid SkuId, string ProductName, string ProductPath, string Color, string Size, string? ImageUrl, int Units, decimal Revenue);
