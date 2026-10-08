using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Inventory.Application;
using Kamus.Orders.Application;
using Kamus.Payments.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Shopping;

/// <summary>
/// O histórico do pedido diz quem fez cada mudança: cliente, loja (Admin) ou sistema. Na coleção
/// sequencial do checkout porque a expiração de reservas é global.
/// </summary>
[Collection(nameof(CheckoutCollection))]
public sealed class OrderActorTests(KamusApiFactory factory)
{
    private const string AdminName = "Administrador Kamus";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Historico_traz_o_cliente_o_FakePay_e_a_loja()
    {
        var (customer, name) = await NewCustomerAsync();
        var sku = await _shop.CreateSkuAsync(stock: 3);
        await Shop.AddToCartAsync(customer, sku.Id);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Approved);
        await Shop.WaitForStatusAsync(customer, placed.Id, "Paid");
        var admin = await _shop.AdminAsync();

        (await admin.PostAsJsonAsync($"/api/admin/orders/{placed.Id}/ship", new ShipOrderRequest("BR123456789BR"), Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/orders/{placed.Id}/deliver", null, Ct)).EnsureSuccessStatusCode();

        var detail = await admin.GetFromJsonAsync<AdminOrderDetail>($"/api/admin/orders/{placed.Id}", Ct);
        detail!.Order.History.Select(h => (h.Status, h.ActorKind, h.ActorName)).Should().Equal(
            ("Created", "Customer", name),
            ("AwaitingPayment", "Customer", name),
            ("Paid", "System", "FakePay"),
            ("Shipped", "Admin", AdminName),
            ("Delivered", "Admin", AdminName));
    }

    [Fact]
    public async Task Cliente_nao_ve_quem_opera_a_loja()
    {
        var (customer, _) = await NewCustomerAsync();
        var placed = await PlaceAsync(customer, FakePayTestCards.Approved);
        await Shop.WaitForStatusAsync(customer, placed.Id, "Paid");

        var order = await Shop.GetOrderAsync(customer, placed.Id);

        order!.History.Should().HaveCount(3).And.OnlyContain(h => h.ActorKind == null && h.ActorName == null);
    }

    [Fact]
    public async Task Pagamento_recusado_fica_no_nome_do_FakePay()
    {
        var (customer, _) = await NewCustomerAsync();
        var placed = await PlaceAsync(customer, FakePayTestCards.Declined);
        await Shop.WaitForStatusAsync(customer, placed.Id, "PaymentFailed");

        var last = (await AdminHistoryAsync(placed.Id))[^1];

        (last.Status, last.ActorKind, last.ActorName).Should().Be(("PaymentFailed", "System", "FakePay"));
    }

    [Fact]
    public async Task Cancelamento_fica_no_nome_de_quem_cancelou()
    {
        var (customer, name) = await NewCustomerAsync();
        var byCustomer = await PlaceAsync(customer, FakePayTestCards.Timeout);
        var byStore = await PlaceAsync(customer, FakePayTestCards.Timeout);
        var admin = await _shop.AdminAsync();

        (await customer.PostAsync($"/api/orders/{byCustomer.Id}/cancel", null, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/admin/orders/{byStore.Id}/cancel", new CancelOrderRequest("Endereço incompleto"), Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        var customerCancel = (await AdminHistoryAsync(byCustomer.Id))[^1];
        (customerCancel.Status, customerCancel.ActorKind, customerCancel.ActorName).Should().Be(("Cancelled", "Customer", name));
        var storeCancel = (await AdminHistoryAsync(byStore.Id))[^1];
        (storeCancel.Status, storeCancel.ActorKind, storeCancel.ActorName).Should().Be(("Cancelled", "Admin", AdminName));
        storeCancel.Note.Should().Be("Cancelado pela loja: Endereço incompleto");
    }

    [Fact]
    public async Task Reserva_vencida_cancela_no_nome_do_sistema()
    {
        var (customer, _) = await NewCustomerAsync();
        var placed = await PlaceAsync(customer, FakePayTestCards.Timeout);

        await factory.Services.GetRequiredService<ReservationExpiryWorker>().RunOnceAsync(DateTimeOffset.UtcNow.AddMinutes(16), Ct);

        var last = (await AdminHistoryAsync(placed.Id))[^1];
        (last.Status, last.ActorKind, last.ActorName).Should().Be(("Cancelled", "System", "Expiração da reserva"));
        last.Note.Should().Be("Tempo para pagamento esgotado.");
    }

    /// <summary>Cliente com nome único: é o nome que aparece no histórico (claim <c>kamus:full_name</c>).</summary>
    private async Task<(HttpClient Client, string Name)> NewCustomerAsync()
    {
        var name = $"Cliente {Guid.NewGuid():N}"[..20];
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"ator-{Guid.NewGuid():N}@kamus.test",
            password = "senha-forte-123",
            fullName = name,
        }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (client, name);
    }

    private async Task<PlacedOrder> PlaceAsync(HttpClient customer, string card)
    {
        var sku = await _shop.CreateSkuAsync(stock: 2);
        await Shop.AddToCartAsync(customer, sku.Id);
        return await Shop.PlaceOrderOkAsync(customer, card);
    }

    private async Task<IReadOnlyList<OrderStatusChangeDto>> AdminHistoryAsync(Guid orderId)
    {
        var admin = await _shop.AdminAsync();
        var detail = await admin.GetFromJsonAsync<AdminOrderDetail>($"/api/admin/orders/{orderId}", Ct);
        return detail!.Order.History;
    }
}
