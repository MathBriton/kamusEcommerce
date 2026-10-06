using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Inventory.Application;
using Kamus.Orders.Application;
using Kamus.Payments.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Shopping;

/// <summary>Testes do fluxo de compra. Rodam em sequência porque a expiração de reservas é global.</summary>
[Collection(nameof(CheckoutCollection))]
public sealed class CheckoutTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Compra_aprovada_baixa_estoque_e_limpa_carrinho()
    {
        var sku = await _shop.CreateSkuAsync(stock: 5, price: 200m);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id, 2);

        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Approved);

        placed.Status.Should().Be("AwaitingPayment");
        placed.Number.Should().StartWith("KM");
        placed.Total.Should().Be(400m + 0m, "acima de R$ 399 o frete é grátis");
        (await _shop.StockAsync(sku.Id)).Should().Be((5, 2), "estoque reservado enquanto o pagamento não confirma");
        (await client.GetFromJsonAsync<CheckoutView>("/api/checkout", Ct))!.Items.Should().BeEmpty("o carrinho é limpo ao criar o pedido");

        var order = await Shop.WaitForStatusAsync(client, placed.Id, "Paid");

        order.History.Select(h => h.Status).Should().Equal("Created", "AwaitingPayment", "Paid");
        (await _shop.StockAsync(sku.Id)).Should().Be((3, 0), "a reserva virou baixa definitiva");
    }

    [Fact]
    public async Task Pagamento_recusado_libera_a_reserva()
    {
        var sku = await _shop.CreateSkuAsync(stock: 3);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id, 3);

        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Declined);
        var order = await Shop.WaitForStatusAsync(client, placed.Id, "PaymentFailed");

        order.History[^1].Note.Should().Contain("não autorizada");
        (await _shop.StockAsync(sku.Id)).Should().Be((3, 0));
    }

    [Fact]
    public async Task Sem_resposta_do_pagamento_a_reserva_expira_e_o_pedido_e_cancelado()
    {
        var sku = await _shop.CreateSkuAsync(stock: 1);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id);

        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Timeout);
        (await _shop.AvailableAsync(sku.Id)).Should().Be(0);

        var worker = factory.Services.GetRequiredService<ReservationExpiryWorker>();
        await worker.RunOnceAsync(DateTimeOffset.UtcNow.AddMinutes(16), Ct);

        var order = await Shop.GetOrderAsync(client, placed.Id);
        order!.Status.Should().Be("Cancelled");
        order.History[^1].Note.Should().Be("Tempo para pagamento esgotado.");
        (await _shop.AvailableAsync(sku.Id)).Should().Be(1, "o item volta a ficar disponível");
    }

    [Fact]
    public async Task Duas_compras_simultaneas_do_ultimo_item_apenas_uma_tem_sucesso()
    {
        var sku = await _shop.CreateSkuAsync(stock: 1);
        var buyers = await Task.WhenAll(_shop.NewCustomerAsync(), _shop.NewCustomerAsync());
        foreach (var buyer in buyers)
        {
            await Shop.AddToCartAsync(buyer, sku.Id);
        }

        var totals = await Task.WhenAll(buyers.Select(Shop.ExpectedTotalAsync));

        // Dispara os dois checkouts ao mesmo tempo
        var responses = await Task.WhenAll(buyers.Select((b, i) => Shop.PlaceOrderAsync(b, FakePayTestCards.Timeout, totals[i])));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        (await loser.Content.ReadAsStringAsync(Ct)).Should().Contain("checkout.insufficient_stock");
        (await _shop.StockAsync(sku.Id)).Should().Be((1, 1), "exatamente uma reserva");
    }

    [Fact]
    public async Task Reservas_concorrentes_nunca_vendem_alem_do_estoque()
    {
        var sku = await _shop.CreateSkuAsync(stock: 5);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var inventory = scope.ServiceProvider.GetRequiredService<Kamus.Inventory.Contracts.IInventoryService>();
            return await inventory.ReserveAsync(Guid.NewGuid(), [new(sku.Id, 1)], TimeSpan.FromMinutes(15), Ct);
        }));

        results.Count(r => r.Succeeded).Should().Be(5);
        (await _shop.StockAsync(sku.Id)).Should().Be((5, 5));
    }

    [Fact]
    public async Task Total_divergente_e_rejeitado()
    {
        var sku = await _shop.CreateSkuAsync(stock: 5, price: 100m);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id);
        var total = await Shop.ExpectedTotalAsync(client);

        await _shop.SetPriceAsync(sku.Id, 130m); // preço sobe enquanto o cliente está no checkout
        var response = await Shop.PlaceOrderAsync(client, FakePayTestCards.Approved, total);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("checkout.total_changed");
        (await _shop.StockAsync(sku.Id)).Should().Be((5, 0), "nada é reservado");
    }

    [Fact]
    public async Task Pedido_guarda_snapshot_de_preco()
    {
        var sku = await _shop.CreateSkuAsync(stock: 5, price: 100m);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id);
        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Timeout);

        await _shop.SetPriceAsync(sku.Id, 999m);

        var order = await Shop.GetOrderAsync(client, placed.Id);
        order!.Items.Single().UnitPrice.Should().Be(100m);
        order.Subtotal.Should().Be(100m);
        order.Shipping.Cost.Should().Be(19.90m);
        order.Total.Should().Be(119.90m);
        order.Address.PostalCode.Should().Be("01310100");
    }

    [Fact]
    public async Task Cliente_cancela_pedido_aguardando_pagamento()
    {
        var sku = await _shop.CreateSkuAsync(stock: 2);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id, 2);
        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Timeout);

        var response = await client.PostAsync($"/api/orders/{placed.Id}/cancel", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Shop.GetOrderAsync(client, placed.Id))!.Status.Should().Be("Cancelled");
        (await _shop.AvailableAsync(sku.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Pedido_pago_avanca_ate_entregue()
    {
        var sku = await _shop.CreateSkuAsync(stock: 2);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id);
        var placed = await Shop.PlaceOrderOkAsync(client, FakePayTestCards.Approved);
        await Shop.WaitForStatusAsync(client, placed.Id, "Paid");

        (await client.PostAsync($"/api/orders/{placed.Id}/advance-fulfillment", null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/orders/{placed.Id}/advance-fulfillment", null, Ct)).EnsureSuccessStatusCode();
        var third = await client.PostAsync($"/api/orders/{placed.Id}/advance-fulfillment", null, Ct);

        third.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Shop.GetOrderAsync(client, placed.Id))!.History.Select(h => h.Status)
            .Should().Equal("Created", "AwaitingPayment", "Paid", "Shipped", "Delivered");
    }

    [Fact]
    public async Task Pedidos_de_outro_cliente_nao_sao_visiveis()
    {
        var sku = await _shop.CreateSkuAsync(stock: 2);
        var owner = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(owner, sku.Id);
        var placed = await Shop.PlaceOrderOkAsync(owner, FakePayTestCards.Timeout);

        var intruder = await _shop.NewCustomerAsync();

        (await intruder.GetAsync($"/api/orders/{placed.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.GetFromJsonAsync<List<OrderSummaryDto>>("/api/orders", Ct)).Should().BeEmpty();
        (await factory.CreateClient().GetAsync("/api/orders", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("1234567812345678")] // Luhn inválido
    [InlineData("4242")]
    public async Task Cartao_invalido_retorna_400(string card)
    {
        var sku = await _shop.CreateSkuAsync(stock: 2);
        var client = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(client, sku.Id);

        var response = await Shop.PlaceOrderAsync(client, card);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Carrinho_vazio_retorna_400()
    {
        var client = await _shop.NewCustomerAsync();

        var response = await Shop.PlaceOrderAsync(client, FakePayTestCards.Approved, expectedTotal: 10m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

[CollectionDefinition(nameof(CheckoutCollection), DisableParallelization = true)]
public sealed class CheckoutCollection;
