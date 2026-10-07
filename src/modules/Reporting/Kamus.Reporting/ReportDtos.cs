namespace Kamus.Reporting;

public sealed record ReportPeriod(DateOnly From, DateOnly To, string TimeZone);

public sealed record SalesKpis(
    decimal Revenue,
    int PaidOrders,
    decimal AverageTicket,
    int UnitsSold,
    int TotalOrders,
    double PaymentFailureRate,
    double CancellationRate);

public sealed record DailySales(DateOnly Date, decimal Revenue, int Orders);

public sealed record StatusCount(string Status, int Count);

public sealed record TopProduct(Guid SkuId, string ProductName, string ProductPath, string Color, string Size, string? ImageUrl, int Units, decimal Revenue);

public sealed record LowStockItem(Guid SkuId, string Code, string ProductName, string Color, string Size, int Quantity, int Reserved, int Available);

public sealed record OverviewReport(
    ReportPeriod Period,
    SalesKpis Kpis,
    IReadOnlyList<DailySales> Daily,
    IReadOnlyList<StatusCount> Statuses,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<LowStockItem> LowStock);
