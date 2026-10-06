using System.Net;
using System.Net.Http.Json;
using Kamus.Cart.Application;
using Kamus.IntegrationTests.Infrastructure;

namespace Kamus.IntegrationTests.Shopping;

public sealed class CartTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Visitante_monta_carrinho_com_cookie()
    {
        var sku = await _shop.CreateSkuAsync(stock: 5, price: 150m, salePrice: 120m);
        var client = factory.CreateClient();

        var add = await client.PostAsJsonAsync("/api/cart/items", new { skuId = sku.Id, quantity = 2 }, Ct);
        add.StatusCode.Should().Be(HttpStatusCode.OK);
        add.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("kamus_vid="));

        var cart = await client.GetFromJsonAsync<CartView>("/api/cart", Ct);
        var item = cart!.Items.Should().ContainSingle().Subject;
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(120m);
        item.ListPrice.Should().Be(150m);
        cart.Subtotal.Should().Be(240m);

        // outro navegador não vê o carrinho
        (await factory.CreateClient().GetFromJsonAsync<CartView>("/api/cart", Ct))!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Nao_adiciona_mais_que_o_estoque()
    {
        var sku = await _shop.CreateSkuAsync(stock: 2);
        var client = factory.CreateClient();

        await Shop.AddToCartAsync(client, sku.Id, 2);
        var more = await client.PostAsJsonAsync("/api/cart/items", new { skuId = sku.Id, quantity = 1 }, Ct);
        var update = await client.PutAsJsonAsync($"/api/cart/items/{sku.Id}", new { quantity = 3 }, Ct);

        more.StatusCode.Should().Be(HttpStatusCode.Conflict);
        update.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetFromJsonAsync<CartView>("/api/cart", Ct))!.Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Atualiza_e_remove_itens()
    {
        var a = await _shop.CreateSkuAsync(stock: 5, price: 50m);
        var b = await _shop.CreateSkuAsync(stock: 5, price: 80m);
        var client = factory.CreateClient();
        await Shop.AddToCartAsync(client, a.Id);
        await Shop.AddToCartAsync(client, b.Id);

        var updated = await client.PutAsJsonAsync($"/api/cart/items/{a.Id}", new { quantity = 3 }, Ct);
        (await updated.Content.ReadFromJsonAsync<CartView>(Ct))!.Subtotal.Should().Be(230m);

        var removed = await client.DeleteAsync($"/api/cart/items/{b.Id}", Ct);
        var cart = (await removed.Content.ReadFromJsonAsync<CartView>(Ct))!;
        cart.Items.Should().ContainSingle(i => i.SkuId == a.Id);
        cart.ItemCount.Should().Be(3);
    }

    [Fact]
    public async Task Sinaliza_preco_alterado_e_estoque_insuficiente()
    {
        var sku = await _shop.CreateSkuAsync(stock: 3, price: 100m);
        var client = factory.CreateClient();
        await Shop.AddToCartAsync(client, sku.Id, 3);

        await _shop.SetPriceAsync(sku.Id, 90m);
        var cart = await client.GetFromJsonAsync<CartView>("/api/cart", Ct);
        cart!.Items.Single().Issue.Should().Be(CartIssues.PriceChanged);
        cart.Items.Single().UnitPrice.Should().Be(90m);
        cart.HasIssues.Should().BeFalse("mudança de preço é só um aviso");

        // outra pessoa compra parte do estoque
        var other = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(other, sku.Id, 2);
        await Shop.PlaceOrderOkAsync(other, Kamus.Payments.Contracts.FakePayTestCards.Timeout);

        cart = await client.GetFromJsonAsync<CartView>("/api/cart", Ct);
        cart!.Items.Single().Issue.Should().Be(CartIssues.InsufficientStock);
        cart.Items.Single().Available.Should().Be(1);
        cart.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task Carrinho_anonimo_e_mesclado_no_login()
    {
        var a = await _shop.CreateSkuAsync(stock: 10);
        var b = await _shop.CreateSkuAsync(stock: 10);
        var email = $"merge-{Guid.NewGuid():N}@kamus.test";

        // cliente já tem 1 unidade de A na conta
        var before = factory.CreateClient();
        await before.PostAsJsonAsync("/api/identity/register", new { email, password = "senha-forte-1", fullName = "Merge" }, Ct);
        await Shop.AddToCartAsync(before, a.Id, 1);

        // em outro navegador, anônimo, coloca A e B
        var browser = factory.CreateClient();
        await Shop.AddToCartAsync(browser, a.Id, 2);
        await Shop.AddToCartAsync(browser, b.Id, 1);

        (await browser.PostAsJsonAsync("/api/identity/login", new { email, password = "senha-forte-1" }, Ct)).EnsureSuccessStatusCode();

        var cart = await browser.GetFromJsonAsync<CartView>("/api/cart", Ct);
        cart!.Items.Should().HaveCount(2);
        cart.Items.Single(i => i.SkuId == a.Id).Quantity.Should().Be(3);
        cart.Items.Single(i => i.SkuId == b.Id).Quantity.Should().Be(1);

        (await browser.PostAsync("/api/identity/logout", null, Ct)).EnsureSuccessStatusCode();
        (await browser.GetFromJsonAsync<CartView>("/api/cart", Ct))!.Items.Should().BeEmpty("o carrinho de visitante foi consumido no login");
    }

    [Fact]
    public async Task Sku_inexistente_retorna_404()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/cart/items", new { skuId = Guid.NewGuid(), quantity = 1 }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
