using System.Net;
using System.Net.Http.Json;
using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.Inventory.Contracts;
using Kamus.Orders.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Infrastructure;

/// <summary>Atalhos para montar cenários de compra nos testes.</summary>
internal sealed class Shop(KamusApiFactory factory)
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static readonly AddressDto Address = new("Maria Silva", "01310-100", "Avenida Paulista", "1000", "Apto 12", "Bela Vista", "São Paulo", "SP");

    /// <summary>Cria um produto com um único SKU e o estoque informado.</summary>
    public async Task<Sku> CreateSkuAsync(int stock, decimal price = 100m, decimal? salePrice = null)
    {
        var catalog = new Catalog.CatalogBuilder(factory);
        var product = catalog.Product($"Peça {Guid.NewGuid():N}"[..14], "roupas", price, salePrice, sizes: ["M"], stock: stock);
        await catalog.SaveAsync();
        return product.Skus[0];
    }

    public async Task<HttpClient> NewCustomerAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"cliente-{Guid.NewGuid():N}@kamus.test",
            password = "senha-forte-123",
            fullName = "Cliente de Teste",
        }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return client;
    }

    /// <summary>Cliente HTTP autenticado como o administrador criado na inicialização (appsettings.Testing).</summary>
    public async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/login", new { email = "admin@kamus.test", password = "admin-teste-123" }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    public static async Task AddToCartAsync(HttpClient client, Guid skuId, int quantity = 1)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new { skuId, quantity }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    public static async Task<decimal> ExpectedTotalAsync(HttpClient client)
    {
        var checkout = await client.GetFromJsonAsync<CheckoutView>("/api/checkout", Ct);
        var shipping = await client.GetFromJsonAsync<ShippingQuoteDto>($"/api/checkout/shipping?state=SP&subtotal={checkout!.Subtotal.ToString(System.Globalization.CultureInfo.InvariantCulture)}", Ct);
        return checkout.Subtotal + shipping!.Cost;
    }

    public static async Task<HttpResponseMessage> PlaceOrderAsync(HttpClient client, string card, decimal? expectedTotal = null)
    {
        var total = expectedTotal ?? await ExpectedTotalAsync(client);
        return await client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(Address, card, total), Ct);
    }

    public static async Task<PlacedOrder> PlaceOrderOkAsync(HttpClient client, string card)
    {
        var response = await PlaceOrderAsync(client, card);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<PlacedOrder>(Ct))!;
    }

    public static Task<OrderDetailDto?> GetOrderAsync(HttpClient client, Guid id) =>
        client.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{id}", Ct);

    /// <summary>Espera o pedido sair de AwaitingPayment (o FakePay responde de forma assíncrona).</summary>
    public static async Task<OrderDetailDto> WaitForStatusAsync(HttpClient client, Guid id, string status)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var order = await GetOrderAsync(client, id);
            if (order!.Status == status || DateTime.UtcNow > deadline)
            {
                order.Status.Should().Be(status);
                return order;
            }

            await Task.Delay(50, Ct);
        }
    }

    public async Task<int> AvailableAsync(Guid skuId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        return (await inventory.GetAvailabilityAsync([skuId], Ct))[skuId];
    }

    public async Task<(int Quantity, int Reserved)> StockAsync(Guid skuId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Kamus.Inventory.Persistence.InventoryDbContext>();
        var level = await db.StockLevels.AsNoTracking().SingleAsync(s => s.SkuId == skuId, Ct);
        return (level.Quantity, level.Reserved);
    }

    public async Task SetPriceAsync(Guid skuId, decimal price)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Skus.Where(s => s.Id == skuId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, price), Ct);
    }
}
