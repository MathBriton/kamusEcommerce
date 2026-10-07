using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.IntegrationTests.Shopping;
using Kamus.Orders.Application;
using Kamus.Payments.Contracts;
using Kamus.Reporting;

namespace Kamus.IntegrationTests.Admin;

/// <summary>Na coleção sequencial do checkout: os relatórios comparam números antes/depois.</summary>
[Collection(nameof(CheckoutCollection))]
public sealed class OrdersAdminTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Loja_despacha_com_rastreio_e_confirma_entrega()
    {
        var admin = await _shop.AdminAsync();
        var (customer, placed) = await PaidOrderAsync(price: 120m, quantity: 1);

        var list = await admin.GetFromJsonAsync<AdminList>($"/api/admin/orders?status=Paid&search={placed.Number}", Ct);
        list!.Items.Should().ContainSingle(o => o.Id == placed.Id).Which.RecipientName.Should().Be("Maria Silva");

        var detail = await admin.GetFromJsonAsync<AdminOrderDetail>($"/api/admin/orders/{placed.Id}", Ct);
        detail!.CustomerEmail.Should().EndWith("@kamus.test");
        detail.PaymentId.Should().NotBeNull();

        (await admin.PostAsJsonAsync($"/api/admin/orders/{placed.Id}/ship", new ShipOrderRequest("br123456789br"), Ct)).EnsureSuccessStatusCode();

        var seenByCustomer = await Shop.GetOrderAsync(customer, placed.Id);
        seenByCustomer!.Status.Should().Be("Shipped");
        seenByCustomer.TrackingCode.Should().Be("BR123456789BR");

        (await admin.PostAsync($"/api/admin/orders/{placed.Id}/deliver", null, Ct)).EnsureSuccessStatusCode();
        (await Shop.GetOrderAsync(customer, placed.Id))!.Status.Should().Be("Delivered");

        // pedido entregue não pode ser cancelado
        var cancel = await admin.PostAsJsonAsync($"/api/admin/orders/{placed.Id}/cancel", new CancelOrderRequest("teste"), Ct);
        cancel.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Loja_cancela_pedido_aguardando_pagamento_e_libera_estoque()
    {
        var admin = await _shop.AdminAsync();
        var sku = await _shop.CreateSkuAsync(stock: 2);
        var customer = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(customer, sku.Id, 2);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Timeout);

        var response = await admin.PostAsJsonAsync($"/api/admin/orders/{placed.Id}/cancel", new CancelOrderRequest("Sem estoque físico"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await Shop.GetOrderAsync(customer, placed.Id);
        order!.Status.Should().Be("Cancelled");
        order.History[^1].Note.Should().Be("Cancelado pela loja: Sem estoque físico");
        (await _shop.AvailableAsync(sku.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Rastreio_invalido_retorna_400()
    {
        var admin = await _shop.AdminAsync();

        var response = await admin.PostAsJsonAsync($"/api/admin/orders/{Guid.NewGuid()}/ship", new ShipOrderRequest("BR 123 <script>"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Relatorio_reflete_os_pedidos_pagos()
    {
        var admin = await _shop.AdminAsync();
        var before = (await admin.GetFromJsonAsync<OverviewReport>("/api/admin/reports/overview", Ct))!;

        var (_, first) = await PaidOrderAsync(price: 150m, quantity: 2);   // 300 + frete 19,90
        var (_, second) = await PaidOrderAsync(price: 500m, quantity: 1);  // frete grátis
        var declinedSku = await _shop.CreateSkuAsync(stock: 1);
        var declinedCustomer = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(declinedCustomer, declinedSku.Id);
        var declined = await Shop.PlaceOrderOkAsync(declinedCustomer, FakePayTestCards.Declined);
        await Shop.WaitForStatusAsync(declinedCustomer, declined.Id, "PaymentFailed");

        var after = (await admin.GetFromJsonAsync<OverviewReport>("/api/admin/reports/overview", Ct))!;

        after.Kpis.Revenue.Should().Be(before.Kpis.Revenue + first.Total + second.Total);
        after.Kpis.PaidOrders.Should().Be(before.Kpis.PaidOrders + 2);
        after.Kpis.UnitsSold.Should().Be(before.Kpis.UnitsSold + 3);
        after.Kpis.TotalOrders.Should().Be(before.Kpis.TotalOrders + 3);
        after.Statuses.Single(s => s.Status == "PaymentFailed").Count
            .Should().Be(before.Statuses.Single(s => s.Status == "PaymentFailed").Count + 1);

        // a série diária soma o mesmo que o KPI e cobre os 30 dias
        after.Daily.Should().HaveCount(30);
        after.Daily.Sum(d => d.Revenue).Should().Be(after.Kpis.Revenue);
        after.Statuses.Sum(s => s.Count).Should().Be(after.Kpis.TotalOrders);
    }

    [Fact]
    public async Task Periodo_invalido_retorna_400()
    {
        var admin = await _shop.AdminAsync();

        var response = await admin.GetAsync("/api/admin/reports/overview?from=2026-10-10&to=2026-10-01", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(HttpClient Customer, PlacedOrder Order)> PaidOrderAsync(decimal price, int quantity)
    {
        var sku = await _shop.CreateSkuAsync(stock: 10, price: price);
        var customer = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(customer, sku.Id, quantity);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Approved);
        await Shop.WaitForStatusAsync(customer, placed.Id, "Paid");
        return (customer, placed);
    }

    private sealed record AdminList(List<AdminOrderRow> Items, int Total);
}
