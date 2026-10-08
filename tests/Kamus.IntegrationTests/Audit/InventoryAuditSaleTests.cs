using System.Net;
using System.Net.Http.Json;
using Kamus.Audit.Api;
using Kamus.Catalog.Contracts;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.IntegrationTests.Shopping;
using Kamus.Inventory.Persistence;
using Kamus.Orders.Application;
using Kamus.Payments.Contracts;
using Kamus.Reporting;
using Kamus.Shared.Auditing;
using Kamus.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Audit;

/// <summary>
/// Estoque mexido por compras: a baixa por venda é auditada; reservar e devolver a reserva não. A venda
/// de um SKU expurgado depois continua nos relatórios (snapshot do pedido). Na coleção sequencial do
/// checkout porque a expiração de reservas é global e os relatórios somam todos os pedidos.
/// </summary>
[Collection(nameof(CheckoutCollection))]
public sealed class InventoryAuditSaleTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Venda_paga_baixa_o_fisico_no_nome_do_FakePay()
    {
        var sku = await _shop.CreateSkuAsync(stock: 5);
        var customer = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(customer, sku.Id, 2);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Approved);
        await Shop.WaitForStatusAsync(customer, placed.Id, "Paid");

        var trail = await InventoryTrailAsync(sku.ProductId);

        trail.Select(e => e.Action).Should().Equal(new[] { "created", "stock_sold" }, "a reserva do checkout não entra na trilha");
        trail[0].Changes.Should().Equal(new AuditChange("Físico", null, "5"));
        var sold = trail[1];
        sold.EntityId.Should().Be(sku.Id);
        sold.Detail.Should().Be($"{sku.Color} · {sku.Size}");
        sold.Changes.Should().Equal(new AuditChange("Físico", "5", "3"));
        sold.Actor.Should().Be(new AuditActorDto("System", null, "FakePay", null));
        (await _shop.StockAsync(sku.Id)).Should().Be((3, 0));
    }

    [Fact]
    public async Task Reserva_e_devolucao_nao_entram_na_trilha()
    {
        var sku = await _shop.CreateSkuAsync(stock: 3);
        var customer = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(customer, sku.Id, 2);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Timeout);
        (await _shop.StockAsync(sku.Id)).Should().Be((3, 2));
        var admin = await _shop.AdminAsync();

        (await admin.PostAsJsonAsync($"/api/admin/orders/{placed.Id}/cancel", new CancelOrderRequest("Teste de reserva"), Ct)).EnsureSuccessStatusCode();

        (await _shop.StockAsync(sku.Id)).Should().Be((3, 0));
        (await InventoryTrailAsync(sku.ProductId)).Select(e => e.Action).Should().Equal("created");
    }

    [Fact]
    public async Task Relatorio_mantem_a_venda_de_SKU_expurgado_pelo_snapshot_do_pedido()
    {
        var sku = await _shop.CreateSkuAsync(stock: 10, price: 10m);
        var customer = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(customer, sku.Id, 10);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Approved);
        var order = await Shop.WaitForStatusAsync(customer, placed.Id, "Paid");

        // Expurgo do Catalog: a linha do SKU some e o evento remove o estoque.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await catalog.Skus.IgnoreQueryFilters().Where(s => s.Id == sku.Id).ExecuteDeleteAsync(Ct);
            using var purge = scope.ServiceProvider.GetRequiredService<ICurrentActor>().ActAs(AuditActor.System("Expurgo automático"));
            await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(new SkusPurged([sku.Id]), Ct);
        }

        var admin = await _shop.AdminAsync();
        var response = await admin.GetAsync("/api/admin/reports/overview", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = (await response.Content.ReadFromJsonAsync<OverviewReport>(Ct))!;
        report.TopProducts.Should().ContainSingle(t => t.SkuId == sku.Id, "10 unidades é mais do que qualquer outro teste compra")
            .Which.Should().Match<TopProduct>(t => t.Units == 10 && t.ProductName == order.Items[0].ProductName);
        report.LowStock.Should().NotContain(i => i.SkuId == sku.Id);
    }

    private async Task<List<AuditEntryDto>> InventoryTrailAsync(Guid productId)
    {
        var admin = await _shop.AdminAsync();
        var entries = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Product/{productId}", Ct);
        return [.. entries!.Where(e => e.Module == InventoryAudit.Module).Reverse()];
    }
}
