using Kamus.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Inventory.Persistence;

internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "inventory";

    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StockLevel>(b =>
        {
            b.HasKey(s => s.SkuId);
            b.Property(s => s.SkuId).ValueGeneratedNever();
            b.Property(s => s.Version).IsRowVersion();
            b.ToTable(t => t.HasCheckConstraint("ck_stock_levels_quantity", "quantity >= 0"));
        });
    }
}
