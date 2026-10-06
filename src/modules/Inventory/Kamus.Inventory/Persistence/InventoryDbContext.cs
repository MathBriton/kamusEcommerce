using Kamus.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Inventory.Persistence;

internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "inventory";

    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StockLevel>(b =>
        {
            b.HasKey(s => s.SkuId);
            b.Property(s => s.SkuId).ValueGeneratedNever();
            b.Property(s => s.Version).IsRowVersion();
            b.Ignore(s => s.Available);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("ck_stock_levels_quantity", "quantity >= 0");
                t.HasCheckConstraint("ck_stock_levels_reserved", "reserved >= 0 AND reserved <= quantity");
            });
        });

        modelBuilder.Entity<Reservation>(b =>
        {
            b.HasIndex(r => r.OrderId).IsUnique();
            b.HasIndex(r => new { r.Status, r.ExpiresAt });
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.OwnsMany(r => r.Lines, l =>
            {
                l.ToTable("reservation_lines");
                l.WithOwner().HasForeignKey("reservation_id");
                l.Property<int>("id");
                l.HasKey("id");
            });
            b.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
