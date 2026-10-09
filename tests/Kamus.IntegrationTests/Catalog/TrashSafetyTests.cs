using System.Net;
using System.Net.Http.Json;
using Kamus.Catalog.Api;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.IntegrationTests.Shopping;
using Kamus.Payments.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Catalog;

/// <summary>Salvaguardas do expurgo: corrida com a restauração e arquivos que os pedidos ainda exibem.</summary>
[Collection(nameof(CheckoutCollection))]
public sealed class TrashSafetyTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TrashFixtures _catalog = new(factory);

    [Fact]
    public async Task Expurgo_nao_apaga_produto_restaurado_enquanto_era_expurgado()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Blusa Seda {Guid.NewGuid():N}"[..20]);
        await TrashFixtures.DeleteProductAsync(admin, product.Id);

        // Quem vai excluir de vez já leu o produto da lixeira...
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var stale = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Skus).Include(p => p.Images)
            .SingleAsync(p => p.Id == product.Id, Ct);

        // ...quando outra pessoa o restaura.
        await TrashFixtures.RestoreOkAsync(admin, "product", product.Id);

        db.Products.Remove(stale);
        var purge = () => db.SaveChangesAsync(Ct);

        await purge.Should().ThrowAsync<DbUpdateConcurrencyException>("o DELETE leva o xmin lido antes da restauração");
        await using var check = factory.Services.CreateAsyncScope();
        var fresh = check.ServiceProvider.GetRequiredService<CatalogDbContext>();
        (await fresh.Products.AnyAsync(p => p.Id == product.Id, Ct)).Should().BeTrue("o produto restaurado continua ativo");
        (await fresh.Skus.CountAsync(s => s.ProductId == product.Id, Ct)).Should().Be(product.Skus.Count);
    }

    [Fact]
    public async Task Expurgo_mantem_a_foto_que_um_pedido_exibe()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Vestido Linho {Guid.NewGuid():N}"[..22]);
        var imageUrl = product.Images.Single().Url;

        var customer = await _catalog.CustomerAsync();
        await Shop.AddToCartAsync(customer, product.Skus[0].Id);
        var placed = await Shop.PlaceOrderOkAsync(customer, FakePayTestCards.Approved);
        var order = await Shop.GetOrderAsync(customer, placed.Id);
        order!.Items.Single().ImageUrl.Should().Be(imageUrl, "o pedido guarda a foto do momento da compra");

        await TrashFixtures.DeleteProductAsync(admin, product.Id);
        (await admin.DeleteAsync($"/api/admin/catalog/trash/product/{product.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _catalog.Store().GetAsync(imageUrl, Ct)).StatusCode.Should().Be(HttpStatusCode.OK,
            "o pedido nunca é excluído e continua mostrando a miniatura");
    }

    [Fact]
    public async Task Imagem_restaurada_vai_para_o_fim_da_galeria_sem_empatar()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Camisa Oxford {Guid.NewGuid():N}"[..22]);
        var first = product.Images.Single();
        (await admin.DeleteAsync($"/api/admin/catalog/products/{product.Id}/images/{first.Id}", Ct)).EnsureSuccessStatusCode();
        var replaced = await TrashFixtures.UploadImageAsync(admin, product.Id, first.Color);
        var second = replaced.Images.Single();
        second.SortOrder.Should().Be(0, "a foto nova ocupa a posição livre");

        await TrashFixtures.RestoreOkAsync(admin, "image", first.Id);

        var detail = await admin.GetFromJsonAsync<AdminProductDetail>($"/api/admin/catalog/products/{product.Id}", Ct);
        detail!.Images.Select(i => (i.Id, i.SortOrder)).Should().Equal((second.Id, 0), (first.Id, 1));
    }

    [Fact]
    public async Task Exclusoes_simultaneas_nao_deixam_produto_publicado_sem_variacao()
    {
        var admin = await _catalog.AdminAsync();
        var other = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Bermuda Sarja {Guid.NewGuid():N}"[..22]);

        var responses = await Task.WhenAll(
            admin.DeleteAsync($"/api/admin/catalog/skus/{product.Skus[0].Id}", Ct),
            other.DeleteAsync($"/api/admin/catalog/skus/{product.Skus[1].Id}", Ct));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict],
            "a segunda exclusão reavalia com dados frescos e encontra a última variação");
        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        (await TrashFixtures.ProblemAsync(conflict)).Code.Should().Be("catalog.last_sku");
        var detail = await admin.GetFromJsonAsync<AdminProductDetail>($"/api/admin/catalog/products/{product.Id}", Ct);
        detail!.IsActive.Should().BeTrue();
        detail.Skus.Should().ContainSingle();
    }
}
