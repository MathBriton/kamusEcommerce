using Kamus.Orders.Contracts;
using Kamus.Reporting;

namespace Kamus.UnitTests;

public sealed class ReportingTests
{
    private static OrderFact Fact(string status, decimal total, int units, string utc) =>
        new(Guid.NewGuid(), DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), status, total, units);

    [Fact]
    public void Kpis_consideram_so_pedidos_vendidos_na_receita()
    {
        var kpis = ReportingService.Kpis(
        [
            Fact("Paid", 100m, 1, "2026-10-01T15:00:00Z"),
            Fact("Delivered", 300m, 3, "2026-10-02T15:00:00Z"),
            Fact("PaymentFailed", 999m, 9, "2026-10-02T16:00:00Z"),
            Fact("Cancelled", 50m, 1, "2026-10-03T16:00:00Z"),
        ]);

        kpis.Revenue.Should().Be(400m);
        kpis.PaidOrders.Should().Be(2);
        kpis.AverageTicket.Should().Be(200m);
        kpis.UnitsSold.Should().Be(4);
        kpis.TotalOrders.Should().Be(4);
        kpis.PaymentFailureRate.Should().BeApproximately(1 / 3d, 0.0001);
        kpis.CancellationRate.Should().Be(0.25);
    }

    [Fact]
    public void Sem_pedidos_nao_divide_por_zero()
    {
        var kpis = ReportingService.Kpis([]);

        kpis.AverageTicket.Should().Be(0);
        kpis.PaymentFailureRate.Should().Be(0);
    }

    [Fact]
    public void Serie_diaria_usa_horario_de_brasilia_e_preenche_dias_vazios()
    {
        var daily = ReportingService.Daily(
        [
            // 01:30 UTC do dia 2 ainda é dia 1 em Brasília (UTC−3)
            Fact("Paid", 100m, 1, "2026-10-02T01:30:00Z"),
            Fact("Shipped", 50m, 1, "2026-10-03T12:00:00Z"),
            Fact("Cancelled", 70m, 1, "2026-10-03T12:00:00Z"),
        ], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 4));

        daily.Select(d => (d.Date.Day, d.Revenue, d.Orders)).Should().Equal((1, 100m, 1), (2, 0m, 0), (3, 50m, 1), (4, 0m, 0));
    }
}
