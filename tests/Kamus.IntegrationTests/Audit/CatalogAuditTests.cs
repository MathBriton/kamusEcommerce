using System.Net;
using System.Net.Http.Json;
using Kamus.Audit.Api;
using Kamus.Catalog.Api;
using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Catalog;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Shared.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Audit;

/// <summary>Trilha de auditoria do catálogo gerada pelas ações do backoffice (módulo "catalog").</summary>
public sealed class CatalogAuditTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TrashFixtures _catalog = new(factory);

    /// <summary>Histórico do produto em ordem cronológica, só com os registros do catálogo.</summary>
    private static async Task<List<AuditEntryDto>> HistoryAsync(HttpClient admin, Guid productId)
    {
        var entries = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Product/{productId}?limit=200", Ct);
        return [.. entries!.Where(e => e.Module == "catalog").Reverse()];
    }

    private async Task<Collection> CollectionAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var slug = $"c{Guid.NewGuid():N}"[..12];
        var collection = new Collection($"Verão {slug}", slug, "Coleção de teste.");
        db.Collections.Add(collection);
        await db.SaveChangesAsync(Ct);
        return collection;
    }

    [Fact]
    public async Task Cada_passo_do_catalogo_vira_registro_com_ator_Admin_e_antes_depois()
    {
        var admin = await _catalog.AdminAsync();
        var categoryId = await _catalog.CategoryIdAsync();
        var collection = await CollectionAsync();
        var name = $"Camisa de Linho {Guid.NewGuid():N}"[..24];

        // criar, editar, adicionar SKUs, publicar, mudar preço, enviar imagem, despublicar
        var product = await _catalog.CreateDraftAsync(admin, name);
        var renamed = $"{name} Areia";
        (await admin.PutAsJsonAsync($"/api/admin/catalog/products/{product.Id}",
            new SaveProductRequest(renamed, product.Description, "Kamus Atelier", categoryId, collection.Id), Ct)).EnsureSuccessStatusCode();
        product = await TrashFixtures.AddSkusAsync(admin, product.Id, "Preto", ["P", "M"], price: 129.90m);
        await TrashFixtures.PublishAsync(admin, product.Id);
        var p = product.Skus.Single(s => s.Size == "P");
        (await admin.PutAsJsonAsync($"/api/admin/catalog/skus/{p.Id}/prices", new UpdateSkuPricesRequest(149.90m, 99.90m), Ct)).EnsureSuccessStatusCode();
        await TrashFixtures.UploadImageAsync(admin, product.Id, "Preto");
        (await admin.PostAsync($"/api/admin/catalog/products/{product.Id}/unpublish", null, Ct)).EnsureSuccessStatusCode();

        // excluir (com filhos), restaurar, excluir de novo e expurgar
        await TrashFixtures.DeleteProductAsync(admin, product.Id);
        await TrashFixtures.RestoreOkAsync(admin, "product", product.Id);
        await TrashFixtures.DeleteProductAsync(admin, product.Id);
        (await admin.DeleteAsync($"/api/admin/catalog/trash/product/{product.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var history = await HistoryAsync(admin, product.Id);

        history.Select(e => (e.EntityType, e.Action)).Should().Equal(
            ("Product", "created"),
            ("Product", "updated"),
            ("Sku", "created"), ("Sku", "created"),
            ("Product", "published"),
            ("Sku", "updated"),
            ("ProductImage", "created"),
            ("Product", "unpublished"),
            ("Product", "deleted"), ("Sku", "deleted"), ("Sku", "deleted"), ("ProductImage", "deleted"),
            ("Product", "restored"), ("Sku", "restored"), ("Sku", "restored"), ("ProductImage", "restored"),
            ("Product", "deleted"), ("Sku", "deleted"), ("Sku", "deleted"), ("ProductImage", "deleted"),
            ("Product", "purged"), ("Sku", "purged"), ("Sku", "purged"), ("ProductImage", "purged"));

        history.Should().AllSatisfy(e =>
        {
            e.SubjectType.Should().Be("Product");
            e.SubjectId.Should().Be(product.Id);
            e.Actor.Kind.Should().Be("Admin");
            e.Actor.Name.Should().Be("Administrador Kamus");
            e.Actor.Email.Should().Be("admin@kamus.test");
            e.CorrelationId.Should().NotBeNullOrEmpty();
        });
        history.Skip(1).Should().OnlyContain(e => e.SubjectLabel == renamed, "o rótulo é o nome do produto no momento");

        var created = history[0];
        created.SubjectLabel.Should().Be(name);
        created.Changes.Should().Contain(new AuditChange("Nome", null, name));
        created.Changes.Should().Contain(new AuditChange("Marca", null, "Kamus Studio"));
        created.Changes.Should().Contain(new AuditChange("Categoria", null, "lixeira"));
        created.Changes.Should().Contain(new AuditChange("Coleção", null, "Nenhuma"));
        created.Changes.Should().Contain(new AuditChange("Situação", null, "Rascunho"));

        history[1].Changes.Should().BeEquivalentTo(new[]
        {
            new AuditChange("Nome", name, renamed),
            new AuditChange("Marca", "Kamus Studio", "Kamus Atelier"),
            new AuditChange("Coleção", "Nenhuma", collection.Name),
        });

        var skuCreated = history[2];
        skuCreated.Detail.Should().Be("Preto · P");
        skuCreated.EntityId.Should().Be(p.Id);
        skuCreated.Changes.Should().BeEquivalentTo(new[]
        {
            new AuditChange("Preço", null, "R$ 129,90"),
            new AuditChange("Código", null, p.Code),
        });

        history[4].Changes.Should().Equal(new AuditChange("Situação", "Rascunho", "Publicado"));
        history[5].Detail.Should().Be("Preto · P");
        history[5].Changes.Should().BeEquivalentTo(new[]
        {
            new AuditChange("Preço", "R$ 129,90", "R$ 149,90"),
            new AuditChange("Promocional", null, "R$ 99,90"),
        });
        history[6].Detail.Should().Be("Cor Preto");
        history[6].Changes.Should().Equal(new AuditChange("Cor", null, "Preto"));
        history[7].Changes.Should().Equal(new AuditChange("Situação", "Publicado", "Rascunho"));

        // exclusão e restauração não mudam campos rastreados; o expurgo registra o último valor
        history.Where(e => e.Action is "deleted" or "restored").Should().OnlyContain(e => e.Changes.Count == 0);
        var purged = history.First(e => e.Action == "purged");
        purged.Changes.Should().Contain(new AuditChange("Nome", renamed, null));
        purged.Changes.Should().Contain(new AuditChange("Situação", "Rascunho", null));
        history.Where(e => e.Action == "deleted").Select(e => e.OccurredAt).Distinct().Should()
            .HaveCount(2, "cada exclusão do produto leva os filhos no mesmo instante");
    }

    [Fact]
    public async Task Excluir_e_restaurar_um_sku_registra_a_variacao_no_historico_do_produto()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Vestido Midi {Guid.NewGuid():N}"[..20]);
        var m = product.Skus.Single(s => s.Size == "M");

        (await admin.DeleteAsync($"/api/admin/catalog/skus/{m.Id}", Ct)).EnsureSuccessStatusCode();
        await TrashFixtures.RestoreOkAsync(admin, "sku", m.Id);

        var history = await HistoryAsync(admin, product.Id);
        var variation = history.Where(e => e.EntityId == m.Id).ToList();
        variation.Select(e => e.Action).Should().Equal("created", "deleted", "restored");
        variation.Should().OnlyContain(e => e.Detail == "Preto · M" && e.SubjectLabel == product.Name);
        history.Should().NotContain(e => e.EntityType == "Product" && e.Action == "updated",
            "mexer só no UpdatedAt do produto não gera registro");
    }
}
