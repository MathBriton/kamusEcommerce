using System.Net;
using System.Net.Http.Json;
using Kamus.Audit.Api;
using Kamus.Catalog.Api;
using Kamus.Catalog.Application;
using Kamus.Catalog.Contracts;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Catalog;

/// <summary>Expurgo da lixeira do catálogo: imediato (botão "Excluir de vez") e automático (retenção).</summary>
public sealed class TrashPurgeTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TrashFixtures _catalog = new(factory);

    private async Task<(int Products, int Skus, int Images)> RowsAsync(Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return (
            await db.Products.IgnoreQueryFilters().CountAsync(p => p.Id == productId, Ct),
            await db.Skus.IgnoreQueryFilters().CountAsync(s => s.ProductId == productId, Ct),
            await db.Images.IgnoreQueryFilters().CountAsync(i => i.ProductId == productId, Ct));
    }

    [Fact]
    public async Task Excluir_de_vez_apaga_produto_com_skus_imagens_e_arquivos()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Jaqueta Jeans {Guid.NewGuid():N}"[..22]);
        var imageUrl = product.Images.Single().Url;
        await TrashFixtures.DeleteProductAsync(admin, product.Id);
        (await _catalog.Store().GetAsync(imageUrl, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        var purge = await admin.DeleteAsync($"/api/admin/catalog/trash/product/{product.Id}", Ct);

        purge.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RowsAsync(product.Id)).Should().Be((0, 0, 0));
        (await _catalog.Store().GetAsync(imageUrl, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound, "o arquivo também é apagado");
        (await TrashFixtures.TrashAsync(admin)).Items.Should().NotContain(i => i.ProductId == product.Id);
        (await TrashFixtures.RestoreAsync(admin, "product", product.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.DeleteAsync($"/api/admin/catalog/trash/product/{product.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Excluir_de_vez_um_sku_ou_imagem_mantem_o_produto()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Saia Midi {Guid.NewGuid():N}"[..18]);
        var (sku, image) = (product.Skus[0], product.Images.Single());
        (await admin.DeleteAsync($"/api/admin/catalog/skus/{sku.Id}", Ct)).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/admin/catalog/products/{product.Id}/images/{image.Id}", Ct)).EnsureSuccessStatusCode();

        (await admin.DeleteAsync($"/api/admin/catalog/trash/sku/{sku.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/admin/catalog/trash/image/{image.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsAsync(product.Id)).Should().Be((1, 1, 0));
        (await _catalog.Store().GetAsync(image.Url, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var detail = await admin.GetFromJsonAsync<AdminProductDetail>($"/api/admin/catalog/products/{product.Id}", Ct);
        detail!.IsActive.Should().BeTrue();
        detail.Skus.Should().ContainSingle().Which.Id.Should().Be(product.Skus[1].Id);
    }

    [Fact]
    public async Task Expurgo_avisa_os_outros_modulos_com_SkusPurged()
    {
        var admin = await _catalog.AdminAsync();
        var product = await _catalog.CreatePublishedAsync(admin, $"Regata Algodão {Guid.NewGuid():N}"[..22]);
        await TrashFixtures.DeleteProductAsync(admin, product.Id);
        var events = new RecordingPublisher();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var trash = ActivatorUtilities.CreateInstance<CatalogTrashService>(scope.ServiceProvider, events);
            (await trash.PurgeAsync("product", product.Id, Ct)).IsSuccess.Should().BeTrue();
        }

        events.Published.Should().ContainSingle().Which.Should().BeOfType<SkusPurged>()
            .Which.SkuIds.Should().BeEquivalentTo(product.Skus.Select(s => s.Id));
    }

    [Fact]
    public async Task Expurgo_automatico_apaga_so_o_que_passou_da_retencao_como_ator_sistema()
    {
        var admin = await _catalog.AdminAsync();
        var expired = await _catalog.CreatePublishedAsync(admin, $"Colete Antigo {Guid.NewGuid():N}"[..22]);
        var recent = await _catalog.CreatePublishedAsync(admin, $"Colete Recente {Guid.NewGuid():N}"[..22]);
        await TrashFixtures.DeleteProductAsync(admin, expired.Id);
        await TrashFixtures.DeleteProductAsync(admin, recent.Id);
        await BackdateAsync(expired.Id, TimeSpan.FromDays(31));

        var purged = await factory.Services.GetRequiredService<TrashPurgeWorker>().RunOnceAsync(Ct);

        purged.Should().BeGreaterThanOrEqualTo(1);
        (await RowsAsync(expired.Id)).Should().Be((0, 0, 0));
        (await RowsAsync(recent.Id)).Should().Be((1, 2, 1), "a retenção padrão é de 30 dias");

        var history = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Product/{expired.Id}", Ct);
        var purges = history!.Where(e => e.Module == "catalog" && e.Action == "purged").ToList();
        purges.Select(e => e.EntityType).Should().BeEquivalentTo(new[] { "Product", "Sku", "Sku", "ProductImage" });
        purges.Should().OnlyContain(e => e.Actor.Kind == "System" && e.Actor.Name == "Expurgo automático" && e.Actor.Id == null);
    }

    /// <summary>Simula um item que está na lixeira há <paramref name="age"/> (sem passar pela auditoria).</summary>
    private async Task BackdateAsync(Guid productId, TimeSpan age)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var deletedAt = DateTimeOffset.UtcNow - age;
        await db.Products.IgnoreQueryFilters().Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, deletedAt), Ct);
        await db.Skus.IgnoreQueryFilters().Where(s => s.ProductId == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, deletedAt), Ct);
        await db.Images.IgnoreQueryFilters().Where(i => i.ProductId == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, deletedAt), Ct);
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<object> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }
    }
}
