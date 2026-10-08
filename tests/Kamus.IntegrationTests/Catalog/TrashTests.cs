using System.Net;
using System.Net.Http.Json;
using Kamus.Cart.Application;
using Kamus.Catalog.Api;
using Kamus.Identity.Api;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Orders.Application;

namespace Kamus.IntegrationTests.Catalog;

/// <summary>Exclusão de produtos, SKUs e imagens (lixeira) e restauração pelo backoffice.</summary>
public sealed class TrashTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TrashFixtures _catalog = new(factory);

    public static TheoryData<string, string> TrashRoutes => new()
    {
        { "GET", "/api/admin/catalog/trash?type=all" },
        { "POST", $"/api/admin/catalog/trash/product/{Guid.NewGuid()}/restore" },
        { "DELETE", $"/api/admin/catalog/trash/product/{Guid.NewGuid()}" },
        { "DELETE", $"/api/admin/catalog/products/{Guid.NewGuid()}" },
        { "DELETE", $"/api/admin/catalog/skus/{Guid.NewGuid()}" },
    };

    [Theory]
    [MemberData(nameof(TrashRoutes))]
    public async Task Rotas_de_exclusao_e_da_lixeira_exigem_Admin(string method, string url)
    {
        var anonymous = await _catalog.Store().SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Ct);
        var customer = await (await _catalog.CustomerAsync()).SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Ct);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        customer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Produto_excluido_some_da_vitrine_do_admin_e_do_carrinho_e_aparece_na_lixeira()
    {
        var admin = await _catalog.AdminAsync();
        var me = await admin.GetFromJsonAsync<MeResponse>("/api/identity/me", Ct);
        var product = await _catalog.CreatePublishedAsync(admin, $"Camisa Lixeira {Guid.NewGuid():N}"[..22]);
        var buyer = await _catalog.CustomerAsync();
        await Shop.AddToCartAsync(buyer, product.Skus[0].Id);

        await TrashFixtures.DeleteProductAsync(admin, product.Id);

        // vitrine: PDP, listagem, facetas e sitemap
        var store = _catalog.Store();
        (await store.GetAsync($"/api/catalog/products/{product.Slug}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await store.GetFromJsonAsync<ProductListPage>($"/api/catalog/products?category={_catalog.Category}", Ct))!.Items.Should().BeEmpty();
        var facets = await store.GetFromJsonAsync<CatalogFacets>($"/api/catalog/facets?category={_catalog.Category}", Ct);
        facets!.Total.Should().Be(0);
        facets.Sizes.Should().BeEmpty();
        (await store.GetFromJsonAsync<List<SitemapEntry>>("/api/catalog/sitemap", Ct)).Should().NotContain(e => e.Path == product.Path);

        // backoffice: lista e detalhe
        var list = await admin.GetFromJsonAsync<AdminPage<AdminProductRow>>($"/api/admin/catalog/products?search={product.Slug}", Ct);
        list!.Items.Should().BeEmpty();
        (await admin.GetAsync($"/api/admin/catalog/products/{product.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // carrinho e checkout: o SKU deixa de ser vendável
        var cart = await buyer.GetFromJsonAsync<CartView>("/api/cart", Ct);
        cart!.Items.Single().Issue.Should().Be(CartIssues.Unavailable);
        cart.HasIssues.Should().BeTrue();
        (await buyer.PostAsJsonAsync("/api/cart/items", new { skuId = product.Skus[1].Id, quantity = 1 }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await buyer.GetFromJsonAsync<CheckoutView>("/api/checkout", Ct))!.CanPlaceOrder.Should().BeFalse();
        var order = await Shop.PlaceOrderAsync(buyer, Kamus.Payments.Contracts.FakePayTestCards.Approved, expectedTotal: product.Skus[0].Price);
        order.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TrashFixtures.ProblemAsync(order)).Code.Should().Be("checkout.item_unavailable");

        // lixeira: só o produto, com os filhos contados nele
        var trash = await TrashFixtures.TrashAsync(admin);
        var item = trash.Items.Should().ContainSingle(i => i.ProductId == product.Id).Subject;
        item.Type.Should().Be("product");
        item.Id.Should().Be(product.Id);
        item.Name.Should().Be(product.Name);
        item.Detail.Should().Be("Kamus Studio · 2 SKUs · 1 imagem");
        item.ImageUrl.Should().Be(product.Images.Single().Url);
        item.SkuCount.Should().Be(2);
        item.ImageCount.Should().Be(1);
        item.WasActive.Should().BeTrue();
        item.DeletedBy.Should().Be(new TrashActorDto(me!.Id, me.FullName));
        item.PurgeAt.Should().Be(item.DeletedAt.AddDays(30));
        trash.Counts.Product.Should().BeGreaterThanOrEqualTo(1);
        (await TrashFixtures.TrashAsync(admin, "sku")).Items.Should().NotContain(i => i.ProductId == product.Id);
        (await TrashFixtures.TrashAsync(admin, "image")).Items.Should().NotContain(i => i.ProductId == product.Id);
        (await TrashFixtures.TrashAsync(admin, "product")).Items.Should().Contain(i => i.Id == product.Id);
    }

    [Fact]
    public async Task Restaurar_produto_devolve_tudo_como_estava()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreateDraftAsync(admin, $"Boné Trucker {Guid.NewGuid():N}"[..20]);
        await TrashFixtures.AddSkusAsync(admin, product.Id, "Preto", ["P", "M", "G"], stock: 6);
        await TrashFixtures.UploadImageAsync(admin, product.Id, "Preto");
        product = await TrashFixtures.PublishAsync(admin, product.Id);
        var g = product.Skus.Single(s => s.Size == "G");

        // o G vai para a lixeira antes do produto: não volta junto com ele
        (await admin.DeleteAsync($"/api/admin/catalog/skus/{g.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        await TrashFixtures.DeleteProductAsync(admin, product.Id);

        var trash = await TrashFixtures.TrashAsync(admin);
        trash.Items.Where(i => i.ProductId == product.Id).Select(i => (i.Type, i.Id)).Should()
            .BeEquivalentTo(new[] { ("product", product.Id), ("sku", g.Id) });
        trash.Items.Single(i => i.Id == product.Id).SkuCount.Should().Be(2);

        var message = await TrashFixtures.RestoreOkAsync(admin, "product", product.Id);

        message.Should().Be($"{product.Name} voltou como estava: publicado, com 2 SKUs e 1 imagem.");
        var restored = await admin.GetFromJsonAsync<AdminProductDetail>($"/api/admin/catalog/products/{product.Id}", Ct);
        restored!.IsActive.Should().BeTrue();
        restored.Skus.Select(s => s.Size).Should().Equal("P", "M");
        restored.Skus.Should().OnlyContain(s => s.Available == 6, "o estoque não muda na exclusão");
        restored.Images.Should().ContainSingle().Which.Url.Should().Be(product.Images.Single().Url);
        var pdp = await _catalog.Store().GetFromJsonAsync<ProductDetail>($"/api/catalog/products/{product.Slug}", Ct);
        pdp!.Colors.Single().Sizes.Select(s => s.Size).Should().Equal("P", "M");
        (await TrashFixtures.TrashAsync(admin)).Items.Should().ContainSingle(i => i.ProductId == product.Id)
            .Which.Type.Should().Be("sku");

        // agora o G pode voltar
        (await TrashFixtures.RestoreOkAsync(admin, "sku", g.Id)).Should().Be($"SKU Preto · G restaurado em {product.Name}, com o estoque que tinha.");
        pdp = await _catalog.Store().GetFromJsonAsync<ProductDetail>($"/api/catalog/products/{product.Slug}", Ct);
        pdp!.Colors.Single().Sizes.Should().HaveCount(3).And.OnlyContain(s => s.Available == 6);
        (await TrashFixtures.TrashAsync(admin)).Items.Should().NotContain(i => i.ProductId == product.Id);
    }

    [Fact]
    public async Task Slug_de_produto_excluido_e_reaproveitado_e_restaurar_gera_conflito()
    {
        var admin = await _catalog.AdminAsync();
        var name = $"Pochete Couro {Guid.NewGuid():N}"[..22];
        var original = await _catalog.CreateDraftAsync(admin, name);
        await TrashFixtures.DeleteProductAsync(admin, original.Id);

        var replacement = await _catalog.CreateDraftAsync(admin, name);
        replacement.Slug.Should().Be(original.Slug, "o slug de um produto na lixeira fica livre");

        var conflict = await TrashFixtures.RestoreAsync(admin, "product", original.Id);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var (code, detail) = await TrashFixtures.ProblemAsync(conflict);
        code.Should().Be("catalog.slug_taken");
        detail.Should().Be($"Já existe outro produto com o endereço /{original.Path}. Renomeie o outro antes de restaurar.");

        await TrashFixtures.DeleteProductAsync(admin, replacement.Id);
        (await TrashFixtures.RestoreOkAsync(admin, "product", original.Id))
            .Should().Be($"{name} voltou como rascunho, sem SKUs nem imagens.");
    }

    [Fact]
    public async Task Ultima_variacao_de_produto_publicado_so_sai_depois_de_despublicar()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Calça Chino {Guid.NewGuid():N}"[..20]);
        var (p, m) = (product.Skus.Single(s => s.Size == "P"), product.Skus.Single(s => s.Size == "M"));

        var first = await admin.DeleteAsync($"/api/admin/catalog/skus/{p.Id}", Ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!.Skus.Should().ContainSingle(s => s.Id == m.Id);
        (await admin.DeleteAsync($"/api/admin/catalog/skus/{p.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var last = await admin.DeleteAsync($"/api/admin/catalog/skus/{m.Id}", Ct);
        last.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TrashFixtures.ProblemAsync(last)).Should().Be(("catalog.last_sku", "Despublique o produto antes de excluir a última variação."));

        (await admin.PostAsync($"/api/admin/catalog/products/{product.Id}/unpublish", null, Ct)).EnsureSuccessStatusCode();
        var afterUnpublish = await admin.DeleteAsync($"/api/admin/catalog/skus/{m.Id}", Ct);
        afterUnpublish.StatusCode.Should().Be(HttpStatusCode.OK);
        (await afterUnpublish.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!.Skus.Should().BeEmpty();
        (await admin.PostAsync($"/api/admin/catalog/products/{product.Id}/publish", null, Ct)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "sem variação ativa não publica");

        var skus = (await TrashFixtures.TrashAsync(admin, "sku")).Items.Where(i => i.ProductId == product.Id).ToList();
        skus.Select(i => i.Id).Should().BeEquivalentTo(new[] { p.Id, m.Id });
        var item = skus.Single(i => i.Id == p.Id);
        item.Name.Should().Be($"{product.Name} · Preto · P");
        item.Detail.Should().Be(p.Code);
        item.ImageUrl.Should().Be(product.Images.Single().Url, "a miniatura é a primeira imagem da cor");
        item.WasActive.Should().BeFalse("o produto não está mais publicado");
    }

    [Fact]
    public async Task Sku_e_imagem_de_produto_na_lixeira_pedem_para_restaurar_o_produto_primeiro()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Viseira Praia {Guid.NewGuid():N}"[..22]);
        var sku = product.Skus[0];
        var image = product.Images.Single();
        (await admin.DeleteAsync($"/api/admin/catalog/skus/{sku.Id}", Ct)).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/admin/catalog/products/{product.Id}/images/{image.Id}", Ct)).EnsureSuccessStatusCode();
        await TrashFixtures.DeleteProductAsync(admin, product.Id);

        foreach (var (type, id) in new[] { ("sku", sku.Id), ("image", image.Id) })
        {
            var response = await TrashFixtures.RestoreAsync(admin, type, id);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await TrashFixtures.ProblemAsync(response)).Should().Be(("catalog.parent_deleted", "Restaure o produto primeiro."));
        }

        (await TrashFixtures.RestoreOkAsync(admin, "product", product.Id))
            .Should().Be($"{product.Name} voltou como estava: publicado, com 1 SKU e nenhuma imagem.");
        (await TrashFixtures.RestoreOkAsync(admin, "image", image.Id))
            .Should().Be($"Imagem restaurada na galeria de {product.Name} (cor Preto).");
    }

    [Fact]
    public async Task Restaurar_sku_com_a_mesma_cor_e_tamanho_ativos_gera_conflito()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreateDraftAsync(admin, $"Meia Cano {Guid.NewGuid():N}"[..18]);
        var old = (await TrashFixtures.AddSkusAsync(admin, product.Id, "Preto", ["U"])).Skus.Single();
        (await admin.DeleteAsync($"/api/admin/catalog/skus/{old.Id}", Ct)).EnsureSuccessStatusCode();

        var replacement = (await TrashFixtures.AddSkusAsync(admin, product.Id, "Preto", ["U"], price: 39.90m)).Skus.Single();
        replacement.Id.Should().NotBe(old.Id);
        replacement.Code.Should().Be(old.Code, "o código de um SKU na lixeira fica livre");

        var conflict = await TrashFixtures.RestoreAsync(admin, "sku", old.Id);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TrashFixtures.ProblemAsync(conflict)).Code.Should().Be("catalog.sku_exists");
    }

    [Fact]
    public async Task Imagem_removida_vai_para_a_lixeira_e_volta_para_a_galeria()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Bolsa Palha {Guid.NewGuid():N}"[..20]);
        var image = product.Images.Single();

        var removed = await admin.DeleteAsync($"/api/admin/catalog/products/{product.Id}/images/{image.Id}", Ct);

        (await removed.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!.Images.Should().BeEmpty();
        var pdp = await _catalog.Store().GetFromJsonAsync<ProductDetail>($"/api/catalog/products/{product.Slug}", Ct);
        pdp!.Colors.Single().Images.Should().BeEmpty();
        (await _catalog.Store().GetAsync(image.Url, Ct)).StatusCode.Should().Be(HttpStatusCode.OK, "o arquivo só sai no expurgo");

        var item = (await TrashFixtures.TrashAsync(admin, "image")).Items.Should().ContainSingle(i => i.ProductId == product.Id).Subject;
        item.Id.Should().Be(image.Id);
        item.Name.Should().Be($"{product.Name} · foto 1");
        item.Detail.Should().Be("Cor Preto");
        item.ImageUrl.Should().Be(image.Url);
        item.WasActive.Should().BeTrue();

        await TrashFixtures.RestoreOkAsync(admin, "image", image.Id);
        pdp = await _catalog.Store().GetFromJsonAsync<ProductDetail>($"/api/catalog/products/{product.Slug}", Ct);
        pdp!.Colors.Single().Images.Should().ContainSingle().Which.Url.Should().Be(image.Url);
    }

    [Fact]
    public async Task Lixeira_valida_o_tipo_e_so_restaura_o_que_esta_nela()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreateDraftAsync(admin, $"Cinto Lona {Guid.NewGuid():N}"[..18]);

        (await admin.GetAsync("/api/admin/catalog/trash?type=pedido", Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TrashFixtures.RestoreAsync(admin, "product", product.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await TrashFixtures.RestoreAsync(admin, "pedido", product.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.DeleteAsync($"/api/admin/catalog/trash/product/{product.Id}", Ct)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "só o que está na lixeira pode ser excluído de vez");
        (await admin.DeleteAsync($"/api/admin/catalog/products/{Guid.NewGuid()}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
