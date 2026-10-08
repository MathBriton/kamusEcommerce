using Kamus.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Orders.Persistence;

internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string Schema = "orders";

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasSequence<long>("order_numbers").StartsAt(10001);

        modelBuilder.Entity<Order>(b =>
        {
            b.Property(o => o.Number).HasDefaultValueSql("nextval('orders.order_numbers')").ValueGeneratedOnAdd();
            b.HasIndex(o => o.Number).IsUnique();
            b.HasIndex(o => new { o.CustomerId, o.CreatedAt });
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(o => o.Subtotal).HasPrecision(10, 2);
            b.Property(o => o.Total).HasPrecision(10, 2);
            b.Property(o => o.Version).IsRowVersion();
            b.Property(o => o.TrackingCode).HasMaxLength(50);
            b.HasIndex(o => new { o.Status, o.CreatedAt });
            b.HasIndex(o => o.CreatedAt);
            b.Ignore(o => o.DisplayNumber);

            b.OwnsOne(o => o.Address, a =>
            {
                a.Property(x => x.RecipientName).HasMaxLength(150);
                a.Property(x => x.PostalCode).HasMaxLength(8);
                a.Property(x => x.Street).HasMaxLength(200);
                a.Property(x => x.Number).HasMaxLength(20);
                a.Property(x => x.Complement).HasMaxLength(100);
                a.Property(x => x.District).HasMaxLength(100);
                a.Property(x => x.City).HasMaxLength(100);
                a.Property(x => x.State).HasMaxLength(2);
            });

            b.OwnsOne(o => o.Shipping, s =>
            {
                s.Property(x => x.Region).HasMaxLength(30);
                s.Property(x => x.Cost).HasPrecision(10, 2);
            });

            b.OwnsMany(o => o.Items, i =>
            {
                i.ToTable("order_items");
                i.WithOwner().HasForeignKey("order_id");
                i.Property<int>("id");
                i.HasKey("id");
                i.Property(x => x.SkuCode).HasMaxLength(50);
                i.Property(x => x.ProductName).HasMaxLength(200);
                i.Property(x => x.ProductPath).HasMaxLength(500);
                i.Property(x => x.Color).HasMaxLength(50);
                i.Property(x => x.Size).HasMaxLength(10);
                i.Property(x => x.ImageUrl).HasMaxLength(500);
                i.Property(x => x.UnitPrice).HasPrecision(10, 2);
                i.Property(x => x.ListPrice).HasPrecision(10, 2);
                i.Ignore(x => x.LineTotal);
            });

            b.OwnsMany(o => o.History, h =>
            {
                h.ToTable("order_status_history");
                h.WithOwner().HasForeignKey("order_id");
                h.Property<int>("id");
                h.HasKey("id");
                h.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                h.Property(x => x.Note).HasMaxLength(300);
                h.Property(x => x.ActorKind).HasMaxLength(20);
                h.Property(x => x.ActorName).HasMaxLength(OrderStatusChange.ActorNameMaxLength);
            });

            b.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(o => o.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
