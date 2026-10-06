using System.Net;
using System.Net.Http.Json;
using Kamus.Catalog.Api;
using Kamus.IntegrationTests.Infrastructure;

namespace Kamus.IntegrationTests.Catalog;

public sealed class ProductListingTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Filtro_por_categoria_inclui_subcategorias()
    {
        var catalog = new CatalogBuilder(factory);
        catalog.Product("Jeans Slim", "masculino/calcas/jeans", 199.90m);
        catalog.Product("Chino", "masculino/calcas/sarja", 179.90m);
        catalog.Product("Camiseta", "masculino/camisetas", 79.90m);
        catalog.Product("Vestido", "feminino/vestidos", 259.90m);
        await catalog.SaveAsync();

        var calcas = await ListAsync($"category={catalog.Path("masculino/calcas")}");
        var masculino = await ListAsync($"category={catalog.Path("masculino")}");

        calcas.Items.Select(i => i.Name).Should().BeEquivalentTo("Jeans Slim", "Chino");
        masculino.Items.Select(i => i.Name).Should().BeEquivalentTo("Jeans Slim", "Chino", "Camiseta");
    }

    [Fact]
    public async Task Filtro_por_tamanho_e_cor()
    {
        var catalog = new CatalogBuilder(factory);
        catalog.Product("Só P azul", "roupas", 100m, colors: [CatalogBuilder.Azul], sizes: ["P"]);
        catalog.Product("G azul e preto", "roupas", 100m, colors: [CatalogBuilder.Azul, CatalogBuilder.Preto], sizes: ["G"]);
        catalog.Product("Bege completo", "roupas", 100m, colors: [CatalogBuilder.Bege], sizes: ["P", "M", "G"]);
        await catalog.SaveAsync();
        var root = $"category={catalog.Root}";

        (await ListAsync($"{root}&size=G")).Items.Select(i => i.Name)
            .Should().BeEquivalentTo("G azul e preto", "Bege completo");

        (await ListAsync($"{root}&color=Azul")).Items.Select(i => i.Name)
            .Should().BeEquivalentTo("Só P azul", "G azul e preto");

        (await ListAsync($"{root}&size=P&size=M")).Items.Select(i => i.Name)
            .Should().BeEquivalentTo("Só P azul", "Bege completo");

        // Cor e tamanho precisam existir no mesmo SKU
        (await ListAsync($"{root}&size=P&color=Preto")).Items.Should().BeEmpty();
        (await ListAsync($"{root}&size=G&color=Preto")).Items.Select(i => i.Name).Should().Equal("G azul e preto");
    }

    [Fact]
    public async Task Filtro_por_faixa_de_preco_considera_preco_promocional()
    {
        var catalog = new CatalogBuilder(factory);
        catalog.Product("Barato", "roupas", 59.90m);
        catalog.Product("Promoção", "roupas", 300m, salePrice: 149.90m);
        catalog.Product("Médio", "roupas", 199.90m);
        catalog.Product("Caro", "roupas", 499.90m);
        await catalog.SaveAsync();

        var page = await ListAsync($"category={catalog.Root}&minPrice=100&maxPrice=200&sort=price_asc");

        page.Items.Select(i => i.Name).Should().Equal("Promoção", "Médio");
        var promo = page.Items[0];
        promo.Price.Should().Be(300m);
        promo.SalePrice.Should().Be(149.90m);
    }

    [Theory]
    [InlineData("newest")]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    [InlineData("name")]
    public async Task Paginacao_por_cursor_percorre_tudo_sem_repetir(string sort)
    {
        var catalog = new CatalogBuilder(factory);
        for (var i = 0; i < 23; i++)
        {
            // preços repetidos forçam o desempate pelo id
            catalog.Product($"Peça {i:D2}", "roupas", 100m + (i % 4 * 10));
        }

        await catalog.SaveAsync();

        var names = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await ListAsync($"category={catalog.Root}&sort={sort}&limit=5" + (cursor is null ? string.Empty : $"&cursor={cursor}"));
            names.AddRange(page.Items.Select(i => i.Name));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null);

        pages.Should().Be(5);
        names.Should().HaveCount(23).And.OnlyHaveUniqueItems();

        var all = await ListAsync($"category={catalog.Root}&sort={sort}&limit=60");
        names.Should().Equal(all.Items.Select(i => i.Name), "paginar deve dar a mesma ordem que uma página única");
    }

    [Fact]
    public async Task Ordenacao_por_preco()
    {
        var catalog = new CatalogBuilder(factory);
        catalog.Product("B", "roupas", 200m);
        catalog.Product("A", "roupas", 300m, salePrice: 99m);
        catalog.Product("C", "roupas", 150m);
        await catalog.SaveAsync();

        (await ListAsync($"category={catalog.Root}&sort=price_asc")).Items.Select(i => i.Name).Should().Equal("A", "C", "B");
        (await ListAsync($"category={catalog.Root}&sort=price_desc")).Items.Select(i => i.Name).Should().Equal("B", "C", "A");
        (await ListAsync($"category={catalog.Root}&sort=newest")).Items.Select(i => i.Name).Should().Equal("C", "A", "B");
    }

    [Fact]
    public async Task Cursor_de_outra_ordenacao_e_rejeitado()
    {
        var catalog = new CatalogBuilder(factory);
        catalog.Product("X", "roupas", 10m);
        catalog.Product("Y", "roupas", 20m);
        await catalog.SaveAsync();

        var page = await ListAsync($"category={catalog.Root}&sort=price_asc&limit=1");

        var response = await _client.GetAsync($"/api/catalog/products?category={catalog.Root}&sort=newest&cursor={page.NextCursor}", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var garbage = await _client.GetAsync("/api/catalog/products?cursor=nao-e-um-cursor", Ct);
        garbage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=61")]
    [InlineData("sort=mais-vendidos")]
    [InlineData("minPrice=300&maxPrice=100")]
    public async Task Parametros_invalidos_retornam_400(string query)
    {
        var response = await _client.GetAsync($"/api/catalog/products?{query}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Facetas_listam_tamanhos_cores_e_precos_da_categoria()
    {
        var catalog = new CatalogBuilder(factory);
        catalog.Product("Calça", "roupas", 200m, colors: [CatalogBuilder.Preto], sizes: ["38", "40"]);
        catalog.Product("Camisa", "roupas", 300m, salePrice: 120m, colors: [CatalogBuilder.Azul, CatalogBuilder.Bege], sizes: ["P", "M"]);
        await catalog.SaveAsync();

        var facets = await _client.GetFromJsonAsync<CatalogFacets>($"/api/catalog/facets?category={catalog.Root}", Ct);

        facets!.Total.Should().Be(2);
        facets.Sizes.Should().BeEquivalentTo("38", "40", "P", "M");
        facets.Colors.Select(c => c.Name).Should().Equal("Azul", "Bege", "Preto");
        facets.MinPrice.Should().Be(120m);
        facets.MaxPrice.Should().Be(200m);
    }

    private async Task<ProductListPage> ListAsync(string query) =>
        (await _client.GetFromJsonAsync<ProductListPage>($"/api/catalog/products?{query}", Ct))!;
}
