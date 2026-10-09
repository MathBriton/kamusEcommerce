using Kamus.Catalog.Domain;
using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Catalog.Persistence;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    /// <summary>Filtro dos índices únicos: só os itens fora da lixeira disputam o valor.</summary>
    private const string ActiveOnly = "deleted_at IS NULL";

    /// <summary>Filtro dos índices da lixeira (poucas linhas: só o que foi excluído).</summary>
    private const string InTrash = "deleted_at IS NOT NULL";

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Collection> Collections => Set<Collection>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Sku> Skus => Set<Sku>();

    public DbSet<ProductImage> Images => Set<ProductImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Ids são gerados na aplicação (Guid v7). Sem isso, o EF trata um SKU ou imagem novos,
        // adicionados a um produto já carregado, como linhas existentes (UPDATE em vez de INSERT).
        modelBuilder.Entity<Category>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<Collection>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<Product>().Property(p => p.Id).ValueGeneratedNever();
        modelBuilder.Entity<Sku>().Property(s => s.Id).ValueGeneratedNever();
        modelBuilder.Entity<ProductImage>().Property(i => i.Id).ValueGeneratedNever();

        modelBuilder.Entity<Category>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100);
            b.Property(c => c.Slug).HasMaxLength(100);
            b.Property(c => c.Path).HasMaxLength(300);
            b.HasIndex(c => c.Path).IsUnique();
            b.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Collection>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100);
            b.Property(c => c.Slug).HasMaxLength(100);
            b.HasIndex(c => c.Slug).IsUnique();
        });

        // Produtos, SKUs e imagens têm lixeira (ADR 0015): o filtro global esconde o que foi
        // excluído e os índices únicos valem só para os ativos, para que um slug ou código possa
        // ser reaproveitado. O índice parcial em deleted_at serve à lixeira e ao expurgo.
        modelBuilder.Entity<Product>(b =>
        {
            b.HasSoftDelete();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(p => p.Name).HasMaxLength(200);
            b.Property(p => p.Slug).HasMaxLength(200);
            b.Property(p => p.Brand).HasMaxLength(100);
            b.HasIndex(p => p.Slug).IsUnique().HasFilter(ActiveOnly);
            b.HasIndex(p => p.DeletedAt).HasFilter(InTrash);
            b.HasIndex(p => new { p.CreatedAt, p.Id });
            b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(p => p.Collection).WithMany().HasForeignKey(p => p.CollectionId).OnDelete(DeleteBehavior.SetNull);
            b.HasMany(p => p.Skus).WithOne().HasForeignKey(s => s.ProductId);
            b.HasMany(p => p.Images).WithOne().HasForeignKey(i => i.ProductId);
            b.Navigation(p => p.Skus).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Sku>(b =>
        {
            b.HasSoftDelete();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(s => s.Code).HasMaxLength(50);
            b.Property(s => s.Color).HasMaxLength(50);
            b.Property(s => s.ColorHex).HasMaxLength(7);
            b.Property(s => s.Size).HasMaxLength(10);
            b.Property(s => s.Price).HasPrecision(10, 2);
            b.Property(s => s.SalePrice).HasPrecision(10, 2);
            b.HasIndex(s => s.Code).IsUnique().HasFilter(ActiveOnly);
            b.HasIndex(s => new { s.ProductId, s.Color, s.Size }).IsUnique().HasFilter(ActiveOnly);
            b.HasIndex(s => s.Size);
            b.HasIndex(s => s.Color);
            b.HasIndex(s => s.DeletedAt).HasFilter(InTrash);
        });

        modelBuilder.Entity<ProductImage>(b =>
        {
            b.ToTable("product_images");
            b.HasSoftDelete();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(i => i.Color).HasMaxLength(50);
            b.Property(i => i.StorageKey).HasMaxLength(300);
            b.Property(i => i.Alt).HasMaxLength(300);
            b.HasIndex(i => i.DeletedAt).HasFilter(InTrash);
        });
    }
}
