using System.Net;
using System.Net.Http.Json;
using Kamus.Audit.Api;
using Kamus.Catalog.Api;
using Kamus.Catalog.Contracts;
using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Inventory.Api;
using Kamus.Inventory.Contracts;
using Kamus.Inventory.Persistence;
using Kamus.Reporting;
using Kamus.Shared.Auditing;
using Kamus.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Audit;

/// <summary>
/// Trilha de auditoria do estoque (módulo "inventory"): só o físico é auditado, com o produto do SKU
/// como agregado ("Nome do produto", detalhe "Cor · Tamanho") resolvido pelo contrato do Catalog.
/// </summary>
public sealed class InventoryAuditTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    [Theory]
    [InlineData(10, 7, 0, 0, "stock_adjusted")]  // ajuste manual
    [InlineData(10, 12, 2, 2, "stock_adjusted")] // ajuste com reserva ativa
    [InlineData(10, 8, 2, 0, "stock_sold")]      // reserva confirmada vira baixa
    [InlineData(10, 10, 0, 2, null)]             // reservar: só o reservado muda
    [InlineData(10, 10, 2, 0, null)]             // liberar ou expirar a reserva
    [InlineData(10, 12, 2, 0, null)]             // combinação atípica: fica "updated"
    public void Acao_do_estoque_depende_de_fisico_e_reservado(int quantityBefore, int quantityAfter, int reservedBefore, int reservedAfter, string? action) =>
        InventoryAudit.Classify(quantityBefore, quantityAfter, reservedBefore, reservedAfter).Should().Be(action);

    [Fact]
    public async Task Estoque_inicial_de_SKU_criado_no_backoffice_leva_o_nome_do_produto()
    {
        var admin = await _shop.AdminAsync();
        var product = await ProductWithSkusAsync(admin, initialStock: 7);

        var trail = await InventoryTrailAsync(admin, product.Id);

        trail.Should().HaveCount(2);
        trail.Should().OnlyContain(e => e.Action == "created" && e.EntityType == "StockLevel");
        trail.Should().OnlyContain(e => e.SubjectType == "Product" && e.SubjectLabel == product.Name,
            "o Catalog grava os SKUs antes de definir o estoque, então o rótulo já resolve");
        trail.Select(e => (e.EntityId, e.Detail)).Should().BeEquivalentTo(product.Skus.Select(s => (s.Id, $"Areia · {s.Size}")));
        trail.Should().OnlyContain(e => e.Changes.SequenceEqual(new[] { new AuditChange("Físico", null, "7") }));
        trail.Should().OnlyContain(e => e.Actor.Kind == "Admin" && e.Actor.Name == "Administrador Kamus");
    }

    [Fact]
    public async Task Ajuste_manual_registra_antes_e_depois_e_ajuste_sem_mudanca_nao_registra()
    {
        var admin = await _shop.AdminAsync();
        var product = await ProductWithSkusAsync(admin, initialStock: 7);
        var sku = product.Skus.Single(s => s.Size == "M");

        (await admin.PutAsJsonAsync($"/api/admin/inventory/skus/{sku.Id}", new AdjustStockRequest(3), Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/inventory/skus/{sku.Id}", new AdjustStockRequest(3), Ct)).EnsureSuccessStatusCode();

        var adjusted = (await InventoryTrailAsync(admin, product.Id)).Where(e => e.Action == "stock_adjusted").ToList();
        adjusted.Should().ContainSingle();
        adjusted[0].EntityId.Should().Be(sku.Id);
        adjusted[0].Detail.Should().Be("Areia · M");
        adjusted[0].SubjectLabel.Should().Be(product.Name);
        adjusted[0].Changes.Should().Equal(new AuditChange("Físico", "7", "3"));
        adjusted[0].Actor.Kind.Should().Be("Admin");
    }

    [Fact]
    public async Task SKUs_expurgados_do_catalogo_perdem_o_estoque()
    {
        var sku = await _shop.CreateSkuAsync(stock: 4);
        await DeleteSkuFromCatalogAsync(sku.Id);

        await PublishPurgeAsync(sku.Id);
        await PublishPurgeAsync(sku.Id); // repetido: idempotente

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            (await db.StockLevels.AnyAsync(s => s.SkuId == sku.Id, Ct)).Should().BeFalse();
        }

        var admin = await _shop.AdminAsync();
        var entries = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Sku/{sku.Id}", Ct);
        var purged = entries.Should().ContainSingle().Which;
        purged.Action.Should().Be("purged");
        purged.EntityType.Should().Be("StockLevel");
        purged.EntityId.Should().Be(sku.Id);
        purged.SubjectLabel.Should().BeNull("o SKU já não existe no catálogo");
        purged.Changes.Should().Equal(new AuditChange("Físico", "4", null));
        purged.Actor.Should().Be(new AuditActorDto("System", null, "Expurgo automático", null));
    }

    [Fact]
    public async Task Relatorio_ignora_estoque_de_SKU_que_nao_existe_mais()
    {
        var ghost = Guid.CreateVersion7();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IInventoryService>().SetStockAsync(new Dictionary<Guid, int> { [ghost] = 1 }, Ct);
        }

        var purgedSku = await _shop.CreateSkuAsync(stock: 1);
        await DeleteSkuFromCatalogAsync(purgedSku.Id);
        var admin = await _shop.AdminAsync();

        var response = await admin.GetAsync("/api/admin/reports/overview", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<OverviewReport>(Ct);
        report!.LowStock.Should().NotContain(i => i.SkuId == ghost || i.SkuId == purgedSku.Id);

        // Estoque de SKU desconhecido do catálogo: o agregado da auditoria cai para o próprio SKU.
        var entries = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Sku/{ghost}", Ct);
        entries.Should().ContainSingle().Which.Should().Match<AuditEntryDto>(e =>
            e.Action == "created" && e.SubjectLabel == null && e.Detail == null && e.Actor.Kind == "System");

        await PublishPurgeAsync(ghost, purgedSku.Id);
    }

    /// <summary>Produto rascunho criado pelo backoffice com SKUs Areia P e M.</summary>
    private async Task<AdminProductDetail> ProductWithSkusAsync(HttpClient admin, int initialStock)
    {
        var categoryId = await NewCategoryAsync();
        var created = await admin.PostAsJsonAsync("/api/admin/catalog/products",
            new SaveProductRequest($"Camisa Estoque {Guid.NewGuid():N}"[..24], "Camisa para a auditoria do estoque.", "Kamus", categoryId, null), Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = (await created.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;

        var withSkus = await admin.PostAsJsonAsync($"/api/admin/catalog/products/{product.Id}/skus",
            new AddSkusRequest("Areia", "#d9c7a7", ["P", "M"], 149.90m, null, initialStock), Ct);
        withSkus.StatusCode.Should().Be(HttpStatusCode.OK, await withSkus.Content.ReadAsStringAsync(Ct));
        return (await withSkus.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
    }

    private async Task<Guid> NewCategoryAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var slug = $"estoque-{Guid.NewGuid():N}"[..20];
        var category = new Category(slug, slug, null, 0);
        db.Categories.Add(category);
        await db.SaveChangesAsync(Ct);
        return category.Id;
    }

    /// <summary>Registros do estoque (módulo "inventory") do produto, em ordem cronológica.</summary>
    private static async Task<List<AuditEntryDto>> InventoryTrailAsync(HttpClient admin, Guid productId)
    {
        var entries = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Product/{productId}", Ct);
        return [.. entries!.Where(e => e.Module == "inventory").Reverse()];
    }

    /// <summary>Simula o expurgo do Catalog: a linha do SKU some de vez (sem passar pelos interceptors).</summary>
    private async Task DeleteSkuFromCatalogAsync(Guid skuId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        (await db.Skus.IgnoreQueryFilters().Where(s => s.Id == skuId).ExecuteDeleteAsync(Ct)).Should().Be(1);
    }

    /// <summary>Publica o evento do expurgo como o worker do Catalog faz: no nome do sistema.</summary>
    private async Task PublishPurgeAsync(params Guid[] skuIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var purge = scope.ServiceProvider.GetRequiredService<ICurrentActor>().ActAs(AuditActor.System("Expurgo automático"));
        await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(new SkusPurged(skuIds), Ct);
    }
}
