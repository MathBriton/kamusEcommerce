using Kamus.Catalog.Contracts;
using Kamus.Inventory.Contracts;
using Kamus.Orders.Contracts;
using Kamus.Shared.Results;

namespace Kamus.Reporting;

/// <summary>
/// Monta os relatórios do backoffice a partir dos contratos de Orders, Inventory e Catalog.
/// Não tem tabelas próprias: na R3 passa a manter read models atualizados por eventos (ADR 0012).
/// </summary>
internal sealed class ReportingService(
    IOrderReports orders,
    IInventoryService inventory,
    ICatalogService catalog,
    TimeProvider clock)
{
    public const int LowStockThreshold = 3;
    public const int MaxDays = 366;

    private static readonly string[] SoldStatuses = ["Paid", "Shipped", "Delivered"];
    private static readonly string[] AllStatuses = ["Created", "AwaitingPayment", "Paid", "Shipped", "Delivered", "Cancelled", "PaymentFailed"];

    /// <summary>Os dias do relatório seguem o horário de Brasília, não UTC.</summary>
    public static readonly TimeZoneInfo Zone = FindZone();

    public async Task<Result<OverviewReport>> OverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);
        var end = to ?? today;
        var start = from ?? end.AddDays(-29);
        if (start > end || end.DayNumber - start.DayNumber + 1 > MaxDays)
        {
            return Error.Validation("reports.invalid_period", $"Período inválido (máximo de {MaxDays} dias).");
        }

        var (fromUtc, toUtc) = (StartOfDayUtc(start), StartOfDayUtc(end.AddDays(1)));
        var facts = await orders.GetOrderFactsAsync(fromUtc, toUtc, ct);
        var top = await orders.GetTopSkusAsync(fromUtc, toUtc, 5, ct);
        var lowStock = await LowStockAsync(ct);

        return new OverviewReport(
            new ReportPeriod(start, end, Zone.Id),
            Kpis(facts),
            Daily(facts, start, end),
            [.. AllStatuses.Select(s => new StatusCount(s, facts.Count(f => f.Status == s)))],
            [.. top.Select(t => new TopProduct(t.SkuId, t.ProductName, t.ProductPath, t.Color, t.Size, t.ImageUrl, t.Units, t.Revenue))],
            lowStock);
    }

    internal static SalesKpis Kpis(IReadOnlyList<OrderFact> facts)
    {
        var sold = facts.Where(f => SoldStatuses.Contains(f.Status)).ToList();
        var revenue = sold.Sum(f => f.Total);
        var failed = facts.Count(f => f.Status == "PaymentFailed");
        var cancelled = facts.Count(f => f.Status == "Cancelled");
        var attempted = sold.Count + failed;

        return new SalesKpis(
            revenue,
            sold.Count,
            sold.Count == 0 ? 0 : Math.Round(revenue / sold.Count, 2),
            sold.Sum(f => f.Units),
            facts.Count,
            attempted == 0 ? 0 : Math.Round((double)failed / attempted, 4),
            facts.Count == 0 ? 0 : Math.Round((double)cancelled / facts.Count, 4));
    }

    internal static IReadOnlyList<DailySales> Daily(IReadOnlyList<OrderFact> facts, DateOnly start, DateOnly end)
    {
        var byDay = facts
            .Where(f => SoldStatuses.Contains(f.Status))
            .GroupBy(f => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(f.CreatedAt, Zone).DateTime))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(f => f.Total), Orders: g.Count()));

        // Dias sem venda aparecem com zero: o gráfico precisa da série completa.
        return [.. Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
            .Select(start.AddDays)
            .Select(d => byDay.TryGetValue(d, out var v) ? new DailySales(d, v.Revenue, v.Orders) : new DailySales(d, 0, 0))];
    }

    private async Task<IReadOnlyList<LowStockItem>> LowStockAsync(CancellationToken ct)
    {
        var levels = await inventory.GetLowStockAsync(LowStockThreshold, 500, ct);
        var skus = await catalog.GetSkusAsync([.. levels.Select(l => l.SkuId)], ct);

        // Só SKUs à venda: o catálogo não devolve rascunhos, itens na lixeira nem SKUs expurgados
        // (o estoque pode existir por um instante a mais que o SKU), e esses ficam de fora sem erro.
        // Empates no disponível são desempatados por nome, cor e tamanho: a lista fica estável.
        var items = new List<LowStockItem>(levels.Count);
        foreach (var level in levels)
        {
            if (skus.TryGetValue(level.SkuId, out var s))
            {
                items.Add(new LowStockItem(level.SkuId, s.Code, s.ProductName, s.Color, s.Size, level.Quantity, level.Reserved, level.Available));
            }
        }

        return [.. items
            .OrderBy(i => i.Available)
            .ThenBy(i => i.ProductName, StringComparer.Ordinal)
            .ThenBy(i => i.Color, StringComparer.Ordinal)
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .Take(12)];
    }

    private static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            // Imagens sem base de fusos: Brasília não tem horário de verão desde 2019.
            return TimeZoneInfo.CreateCustomTimeZone("America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");
        }
    }
}
