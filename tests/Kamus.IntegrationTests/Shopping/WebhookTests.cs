using System.Net;
using System.Text;
using System.Text.Json;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Payments.Contracts;
using Kamus.Payments.FakePay;
using Kamus.Payments.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Shopping;

[Collection(nameof(CheckoutCollection))]
public sealed class WebhookTests(KamusApiFactory factory)
{
    private const string Secret = "whsec_dev_kamus_fakepay";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Webhook_duplicado_nao_tem_efeito_colateral()
    {
        // Cartão "timeout": o FakePay não responde sozinho, então o teste controla os webhooks.
        var sku = await _shop.CreateSkuAsync(stock: 4);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id, 2);
        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Timeout);
        var payment = await PaymentForAsync(placed.Id);

        var body = Event($"evt_{Guid.NewGuid():N}", FakePayWebhookEvent.ChargeSucceeded, payment.Id, payment.ProviderChargeId!, payment.Amount);

        var first = await SendAsync(body);
        // a mesma entrega repetida, inclusive em paralelo
        var repeats = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendAsync(body)));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        repeats.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        foreach (var response in repeats)
        {
            (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("\"duplicate\":true");
        }

        var order = await Shop.GetOrderAsync(client, placed.Id);
        order!.Status.Should().Be("Paid");
        order.History.Count(h => h.Status == "Paid").Should().Be(1);
        (await _shop.StockAsync(sku.Id)).Should().Be((2, 0), "o estoque baixou uma única vez");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        (await db.ProcessedWebhookEvents.CountAsync(e => e.EventId == JsonDocument.Parse(body).RootElement.GetProperty("id").GetString(), Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Cenario_de_webhook_duplicado_do_fakepay_paga_uma_vez()
    {
        var sku = await _shop.CreateSkuAsync(stock: 3);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id);

        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.DuplicateWebhook);
        var order = await Shop.WaitForStatusAsync(client, placed.Id, "Paid");
        await Task.Delay(500, Ct); // dá tempo para as entregas repetidas chegarem

        order = await Shop.GetOrderAsync(client, placed.Id);
        order!.History.Count(h => h.Status == "Paid").Should().Be(1);
        (await _shop.StockAsync(sku.Id)).Should().Be((2, 0));
    }

    [Fact]
    public async Task Assinatura_invalida_retorna_401()
    {
        var body = Event("evt_falso", FakePayWebhookEvent.ChargeSucceeded, Guid.NewGuid(), "ch_x", 10m);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/fakepay")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(FakePaySignature.Header, FakePaySignature.Sign(body, "segredo-errado", DateTimeOffset.UtcNow));

        var response = await factory.CreateClient().SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Aprovacao_depois_da_expiracao_cancela_o_pedido()
    {
        var sku = await _shop.CreateSkuAsync(stock: 1);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id);
        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Timeout);
        var payment = await PaymentForAsync(placed.Id);

        var worker = factory.Services.GetRequiredService<Kamus.Inventory.Application.ReservationExpiryWorker>();
        await worker.RunOnceAsync(DateTimeOffset.UtcNow.AddMinutes(16), Ct);

        // o banco aprova tarde demais
        var response = await SendAsync(Event($"evt_{Guid.NewGuid():N}", FakePayWebhookEvent.ChargeSucceeded, payment.Id, payment.ProviderChargeId!, payment.Amount));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Shop.GetOrderAsync(client, placed.Id))!.Status.Should().Be("Cancelled");
        (await _shop.StockAsync(sku.Id)).Should().Be((1, 0), "o estoque não pode ser baixado sem reserva");
    }

    private async Task<HttpResponseMessage> SendAsync(string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/fakepay")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(FakePaySignature.Header, FakePaySignature.Sign(body, Secret, DateTimeOffset.UtcNow));
        return await factory.CreateClient().SendAsync(request, Ct);
    }

    private static string Event(string id, string type, Guid reference, string chargeId, decimal amount) =>
        JsonSerializer.Serialize(new
        {
            id,
            type,
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new { chargeId, reference, amount, failureReason = (string?)null },
        });

    private async Task<Kamus.Payments.Domain.Payment> PaymentForAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        return await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == orderId, Ct);
    }
}
