using Kamus.Catalog.Domain;
using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Catalog.Persistence;

/// <summary>
/// O que o catálogo grava na trilha de auditoria (módulo <c>catalog</c>). Produto, SKU e imagem
/// aparecem sempre sob o produto (o agregado exibido no histórico); só os campos com <c>Track</c>
/// viram "antes → depois".
/// </summary>
internal static class CatalogAuditPolicy
{
    public const string Module = "catalog";

    public const string ProductSubject = "Product";

    public static void Configure(AuditPolicyBuilder audit) => audit
        .Module(Module)
        // O produto vem antes dos filhos: num mesmo SaveChanges (ex.: excluir com SKUs e imagens),
        // os registros saem nessa ordem.
        .Entity<Product>(e => e
            .Subject(ProductSubject, p => p.Id, p => p.Name)
            .Track(p => p.Name, "Nome")
            .Track(p => p.Description, "Descrição")
            .Track(p => p.Brand, "Marca")
            .Track(p => p.CategoryId, "Categoria", CategoryNameAsync)
            .Track(p => p.CollectionId, "Coleção", CollectionNameAsync)
            .Track(p => p.IsActive, "Situação", v => v is true ? "Publicado" : "Rascunho")
            .Action(entry => entry.HasChanged(p => p.IsActive)
                ? (entry.Entity.IsActive ? "published" : "unpublished")
                : null))
        .Entity<Sku>(e => e
            .SubjectAsync(ProductSubject, async (sp, sku, ct) => (sku.ProductId, await ProductNameAsync(sp, sku.ProductId, ct), null))
            .Detail(s => $"{s.Color} · {s.Size}")
            .Track(s => s.Price, "Preço")
            .Track(s => s.SalePrice, "Promocional")
            .TrackOnCreate(s => s.Code, "Código"))
        .Entity<ProductImage>(e => e
            .SubjectAsync(ProductSubject, async (sp, image, ct) => (image.ProductId, await ProductNameAsync(sp, image.ProductId, ct), null))
            .Detail(i => $"Cor {i.Color}")
            .Track(i => i.Color, "Cor"));

    /// <summary>
    /// Nome do produto para o rótulo de SKUs e imagens. Roda antes de gravar: primeiro o change
    /// tracker (produto criado ou renomeado agora, ou expurgado junto), depois o banco ignorando o
    /// filtro da lixeira.
    /// </summary>
    private static async ValueTask<string?> ProductNameAsync(IServiceProvider services, Guid productId, CancellationToken ct)
    {
        var db = services.GetRequiredService<CatalogDbContext>();
        var tracked = db.ChangeTracker.Entries<Product>().FirstOrDefault(p => p.Entity.Id == productId);
        if (tracked is not null)
        {
            return tracked.Entity.Name;
        }

        return await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);
    }

    private static async ValueTask<string?> CategoryNameAsync(DbContext db, object? value, CancellationToken ct)
    {
        if (value is not Guid id)
        {
            return null;
        }

        // Categoria criada no mesmo SaveChanges (ainda não está no banco).
        var tracked = db.ChangeTracker.Entries<Category>().FirstOrDefault(c => c.Entity.Id == id);
        return tracked?.Entity.Name
            ?? await db.Set<Category>().AsNoTracking().Where(c => c.Id == id).Select(c => c.Name).FirstOrDefaultAsync(ct)
            ?? id.ToString();
    }

    private static async ValueTask<string?> CollectionNameAsync(DbContext db, object? value, CancellationToken ct)
    {
        if (value is not Guid id)
        {
            return "Nenhuma";
        }

        var tracked = db.ChangeTracker.Entries<Collection>().FirstOrDefault(c => c.Entity.Id == id);
        return tracked?.Entity.Name
            ?? await db.Set<Collection>().AsNoTracking().Where(c => c.Id == id).Select(c => c.Name).FirstOrDefaultAsync(ct)
            ?? id.ToString();
    }
}
