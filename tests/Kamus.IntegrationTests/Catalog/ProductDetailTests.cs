using System.Net;
using System.Net.Http.Json;
using Kamus.Catalog.Api;
using Kamus.IntegrationTests.Infrastructure;

namespace Kamus.IntegrationTests.Catalog;

public sealed class ProductDetailTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Pdp_traz_breadcrumb_cores_e_disponibilidade_por_tamanho()
    {
        var catalog = new CatalogBuilder(factory);
        var product = catalog.Product("Jeans Slim Azul", "masculino/calcas/jeans", 249.90m, salePrice: 199.90m,
            colors: [CatalogBuilder.Azul, CatalogBuilder.Preto], sizes: ["38", "40"], stock: 3);
        catalog.SetStock(product.Skus.Single(s => s.Color == "Preto" && s.Size == "40"), 0);
        await catalog.SaveAsync();

        var detail = await _client.GetFromJsonAsync<ProductDetail>($"/api/catalog/products/{product.Slug}", Ct);

        detail!.Path.Should().Be($"{catalog.Path("masculino/calcas/jeans")}/{product.Slug}");
        detail.Breadcrumb.Select(b => b.Path).Should().Equal(catalog.Root, catalog.Path("masculino"), catalog.Path("masculino/calcas"), catalog.Path("masculino/calcas/jeans"));
        detail.Price.Should().Be(249.90m);
        detail.SalePrice.Should().Be(199.90m);
        detail.Colors.Select(c => c.Name).Should().BeEquivalentTo("Azul", "Preto");

        var preto = detail.Colors.Single(c => c.Name == "Preto");
        preto.Images.Should().ContainSingle().Which.Url.Should().StartWith("/files/products/");
        preto.Sizes.Select(s => (s.Size, s.Available)).Should().Equal(("38", 3), ("40", 0));
    }

    [Fact]
    public async Task Produto_inexistente_retorna_404()
    {
        var response = await _client.GetAsync("/api/catalog/products/nao-existe", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Arquivos_bloqueiam_path_traversal()
    {
        var response = await _client.GetAsync("/files/..%2F..%2Fappsettings.json", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
