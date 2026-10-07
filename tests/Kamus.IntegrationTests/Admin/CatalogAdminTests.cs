using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.Catalog.Api;
using Kamus.IntegrationTests.Infrastructure;

namespace Kamus.IntegrationTests.Admin;

public sealed class CatalogAdminTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Assinatura mínima de PNG seguida de bytes quaisquer: o servidor só olha o cabeçalho.
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3, 4];

    private readonly Shop _shop = new(factory);

    [Fact]
    public async Task Produto_criado_no_admin_aparece_na_loja()
    {
        var admin = await _shop.AdminAsync();
        var categoryId = await CategoryAsync("masculino/camisetas");

        var created = await admin.PostAsJsonAsync("/api/admin/catalog/products",
            new SaveProductRequest("Camiseta Admin Linho", "Camiseta criada pelo backoffice.", "Kamus", categoryId, null), Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = (await created.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
        product.IsActive.Should().BeFalse("produto novo nasce como rascunho");

        // sem SKU não publica
        (await admin.PostAsync($"/api/admin/catalog/products/{product.Id}/publish", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var withSkus = await admin.PostAsJsonAsync($"/api/admin/catalog/products/{product.Id}/skus",
            new AddSkusRequest("Areia", "#d9c7a7", ["M", "P", "g"], 149.90m, null, 7), Ct);
        product = (await withSkus.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
        product.Skus.Select(s => s.Size).Should().Equal("P", "M", "G");
        product.Skus.Should().OnlyContain(s => s.Available == 7);

        (await admin.PostAsync($"/api/admin/catalog/products/{product.Id}/publish", null, Ct)).EnsureSuccessStatusCode();

        var store = factory.CreateClient();
        var pdp = await store.GetFromJsonAsync<ProductDetail>($"/api/catalog/products/{product.Slug}", Ct);
        pdp!.Path.Should().Be($"masculino/camisetas/{product.Slug}");
        pdp.Colors.Single().Sizes.Should().OnlyContain(s => s.Available == 7);

        // preço promocional
        var sku = product.Skus[0];
        (await admin.PutAsJsonAsync($"/api/admin/catalog/skus/{sku.Id}/prices", new UpdateSkuPricesRequest(149.90m, 99.90m), Ct)).EnsureSuccessStatusCode();
        pdp = await store.GetFromJsonAsync<ProductDetail>($"/api/catalog/products/{product.Slug}", Ct);
        pdp!.Colors.Single().Sizes.Single(s => s.SkuId == sku.Id).SalePrice.Should().Be(99.90m);

        // despublicar tira da vitrine
        (await admin.PostAsync($"/api/admin/catalog/products/{product.Id}/unpublish", null, Ct)).EnsureSuccessStatusCode();
        (await store.GetAsync($"/api/catalog/products/{product.Slug}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // a lista do admin continua mostrando o rascunho
        var list = await admin.GetFromJsonAsync<AdminPage<AdminProductRow>>("/api/admin/catalog/products?search=Admin%20Linho&status=inactive", Ct);
        list!.Items.Should().ContainSingle(p => p.Id == product.Id).Which.Available.Should().Be(21);
    }

    [Fact]
    public async Task Slug_duplicado_ganha_sufixo()
    {
        var admin = await _shop.AdminAsync();
        var categoryId = await CategoryAsync("feminino/vestidos");
        var request = new SaveProductRequest($"Vestido Repetido {Guid.NewGuid():N}"[..24], "Descrição", "Kamus", categoryId, null);

        var first = await (await admin.PostAsJsonAsync("/api/admin/catalog/products", request, Ct)).Content.ReadFromJsonAsync<AdminProductDetail>(Ct);
        var second = await (await admin.PostAsJsonAsync("/api/admin/catalog/products", request, Ct)).Content.ReadFromJsonAsync<AdminProductDetail>(Ct);

        second!.Slug.Should().Be($"{first!.Slug}-2");
    }

    [Fact]
    public async Task Upload_de_imagem_valida_o_conteudo()
    {
        var admin = await _shop.AdminAsync();
        var product = await DraftWithSkuAsync(admin, "Preto");

        var ok = await UploadAsync(admin, product.Id, "Preto", Png, "foto.png");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var image = (await ok.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!.Images.Single();
        image.Url.Should().EndWith(".png");
        (await factory.CreateClient().GetAsync(image.Url, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // conteúdo SVG com extensão .png: rejeitado pelo conteúdo
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        (await UploadAsync(admin, product.Id, "Preto", svg, "foto.png")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // cor sem SKU
        (await UploadAsync(admin, product.Id, "Verde", Png, "foto.png")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var removed = await admin.DeleteAsync($"/api/admin/catalog/products/{product.Id}/images/{image.Id}", Ct);
        (await removed.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task Preco_promocional_invalido_retorna_400()
    {
        var admin = await _shop.AdminAsync();
        var product = await DraftWithSkuAsync(admin, "Preto");

        var response = await admin.PutAsJsonAsync($"/api/admin/catalog/skus/{product.Skus[0].Id}/prices", new UpdateSkuPricesRequest(100m, 120m), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ajuste_de_estoque_respeita_reservas()
    {
        var admin = await _shop.AdminAsync();
        var sku = await _shop.CreateSkuAsync(stock: 5);
        var buyer = await _shop.NewCustomerAsync();
        await Shop.AddToCartAsync(buyer, sku.Id, 3);
        await Shop.PlaceOrderOkAsync(buyer, Kamus.Payments.Contracts.FakePayTestCards.Timeout);

        var below = await admin.PutAsJsonAsync($"/api/admin/inventory/skus/{sku.Id}", new { quantity = 2 }, Ct);
        var ok = await admin.PutAsJsonAsync($"/api/admin/inventory/skus/{sku.Id}", new { quantity = 10 }, Ct);

        below.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _shop.StockAsync(sku.Id)).Should().Be((10, 3));
    }

    private async Task<AdminProductDetail> DraftWithSkuAsync(HttpClient admin, string color)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/catalog/products",
            new SaveProductRequest($"Peça {Guid.NewGuid():N}"[..14], "Descrição", "Kamus", await CategoryAsync("acessorios/bolsas"), null), Ct);
        var product = (await created.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
        var withSku = await admin.PostAsJsonAsync($"/api/admin/catalog/products/{product.Id}/skus",
            new AddSkusRequest(color, "#1f1f1f", ["U"], 100m, null, 1), Ct);
        return (await withSku.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient admin, Guid productId, string color, byte[] bytes, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(color), "color");
        return await admin.PostAsync($"/api/admin/catalog/products/{productId}/images", form, Ct);
    }

    /// <summary>Garante a árvore de categorias do seed (os testes não rodam o seed de catálogo).</summary>
    private async Task<Guid> CategoryAsync(string path)
    {
        var tree = await factory.CreateClient().GetFromJsonAsync<List<CategoryNode>>("/api/catalog/categories", Ct);
        var found = Flatten(tree!).FirstOrDefault(c => c.Path == path);
        if (found is not null)
        {
            return found.Id;
        }

        await new FixedCategories(factory).EnsureAsync(path);
        return await CategoryAsync(path);
    }

    private static IEnumerable<CategoryNode> Flatten(IEnumerable<CategoryNode> nodes) =>
        nodes.SelectMany(n => Flatten(n.Children).Prepend(n));
}
