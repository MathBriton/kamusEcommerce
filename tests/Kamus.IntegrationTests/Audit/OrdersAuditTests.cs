using System.Net;
using System.Net.Http.Json;
using Kamus.Audit.Api;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.IntegrationTests.Shopping;
using Kamus.Orders.Application;
using Kamus.Payments.Contracts;
using Kamus.Shared.Auditing;

namespace Kamus.IntegrationTests.Audit;

/// <summary>
/// Trilha de auditoria dos pedidos (módulo "orders"): cada mudança de situação vira um registro com
/// ator e "antes → depois". Na coleção sequencial do checkout porque a expiração de reservas é global.
/// </summary>
[Collection(nameof(CheckoutCollection))]
public sealed class OrdersAuditTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Pedido_criado_pago_despachado_e_entregue_fica_na_trilha()
    {
        var name = $"Comprador {Guid.NewGuid():N}"[..22];
        var customer = await RegisterAsync(name);
        var sku = await _shop.CreateSkuAsync(stock: 4);
        await Shop.AddToCartAsync(customer, sku.Id);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Approved);
        await Shop.WaitForStatusAsync(customer, placed.Id, "Paid");
        var admin = await _shop.AdminAsync();
        (await admin.PostAsJsonAsync($"/api/admin/orders/{placed.Id}/ship", new ShipOrderRequest("br123456789br"), Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/orders/{placed.Id}/deliver", null, Ct)).EnsureSuccessStatusCode();

        var trail = await TrailAsync(admin, placed.Id);

        trail.Select(e => e.Action).Should().Equal("created", "paid", "shipped", "delivered");
        trail.Should().OnlyContain(e => e.Module == "orders" && e.EntityType == "Order" && e.EntityId == placed.Id);
        trail.Should().OnlyContain(e => e.SubjectLabel == $"Pedido {placed.Number}", "o número gerado no insert entra no rótulo");
        trail.Should().OnlyContain(e => e.Detail == Shop.Address.RecipientName);

        var created = trail[0];
        created.Changes.Should().Equal(new AuditChange("Situação", null, "Aguardando pagamento"));
        created.Actor.Kind.Should().Be("Customer");
        created.Actor.Name.Should().Be(name);
        created.Actor.Id.Should().NotBeNull();
        created.Actor.Email.Should().EndWith("@kamus.test");

        trail[1].Changes.Should().Equal(new AuditChange("Situação", "Aguardando pagamento", "Pago"));
        trail[1].Actor.Should().Be(new AuditActorDto("System", null, "FakePay", null));

        trail[2].Changes.Should().Equal(
            new AuditChange("Situação", "Pago", "Enviado"),
            new AuditChange("Rastreio", null, "BR123456789BR"));
        trail[2].Actor.Kind.Should().Be("Admin");
        trail[2].Actor.Name.Should().Be("Administrador Kamus");

        trail[3].Changes.Should().Equal(new AuditChange("Situação", "Enviado", "Entregue"));
    }

    [Fact]
    public async Task Pagamento_recusado_e_cancelamento_tem_acao_propria()
    {
        var customer = await RegisterAsync("Cliente Recusado");
        var declinedSku = await _shop.CreateSkuAsync(stock: 2);
        await Shop.AddToCartAsync(customer, declinedSku.Id);
        var declined = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Declined);
        await Shop.WaitForStatusAsync(customer, declined.Id, "PaymentFailed");

        var pendingSku = await _shop.CreateSkuAsync(stock: 2);
        await Shop.AddToCartAsync(customer, pendingSku.Id);
        var pending = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Timeout);
        var admin = await _shop.AdminAsync();
        (await admin.PostAsJsonAsync($"/api/admin/orders/{pending.Id}/cancel", new CancelOrderRequest("Pedido em duplicidade"), Ct)).EnsureSuccessStatusCode();

        var failed = (await TrailAsync(admin, declined.Id))[^1];
        failed.Action.Should().Be("payment_failed");
        failed.Changes.Should().Equal(new AuditChange("Situação", "Aguardando pagamento", "Pagamento recusado"));
        failed.Actor.Name.Should().Be("FakePay");

        var cancelled = (await TrailAsync(admin, pending.Id))[^1];
        cancelled.Action.Should().Be("cancelled");
        cancelled.Changes.Should().Equal(new AuditChange("Situação", "Aguardando pagamento", "Cancelado"));
        cancelled.Actor.Kind.Should().Be("Admin");
    }

    [Fact]
    public async Task Filtro_por_modulo_e_acao_encontra_o_despacho()
    {
        var customer = await RegisterAsync("Cliente Filtro");
        var sku = await _shop.CreateSkuAsync(stock: 2);
        await Shop.AddToCartAsync(customer, sku.Id);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Approved);
        await Shop.WaitForStatusAsync(customer, placed.Id, "Paid");
        var admin = await _shop.AdminAsync();
        (await admin.PostAsJsonAsync($"/api/admin/orders/{placed.Id}/ship", new ShipOrderRequest("QB000111222BR"), Ct)).EnsureSuccessStatusCode();

        var page = await admin.GetFromJsonAsync<AuditEntriesPage>("/api/admin/audit/entries?module=orders&action=shipped&pageSize=100", Ct);

        page!.Items.Should().Contain(e => e.SubjectId == placed.Id && e.SubjectType == "Order")
            .Which.Changes.Should().Contain(new AuditChange("Rastreio", null, "QB000111222BR"));
    }

    /// <summary>Registros do pedido em ordem cronológica (a API devolve os mais recentes primeiro).</summary>
    private static async Task<List<AuditEntryDto>> TrailAsync(HttpClient admin, Guid orderId)
    {
        var entries = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Order/{orderId}", Ct);
        return [.. entries!.AsEnumerable().Reverse()];
    }

    private async Task<HttpClient> RegisterAsync(string fullName)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"auditoria-{Guid.NewGuid():N}@kamus.test",
            password = "senha-forte-123",
            fullName,
        }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return client;
    }
}
