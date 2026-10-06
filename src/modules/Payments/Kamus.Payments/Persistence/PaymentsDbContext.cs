using Kamus.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Payments.Persistence;

internal sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Payment>(b =>
        {
            b.Property(p => p.Amount).HasPrecision(10, 2);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.OrderNumber).HasMaxLength(20);
            b.Property(p => p.CardLast4).HasMaxLength(4);
            b.Property(p => p.Provider).HasMaxLength(30);
            b.Property(p => p.ProviderChargeId).HasMaxLength(100);
            b.Property(p => p.FailureReason).HasMaxLength(300);
            b.Property(p => p.Version).IsRowVersion();
            b.Ignore(p => p.IsFinal);
            b.HasIndex(p => p.OrderId);
            b.HasIndex(p => p.ProviderChargeId).IsUnique();
            b.HasIndex(p => new { p.Status, p.OrderNotifiedAt });
        });

        modelBuilder.Entity<ProcessedWebhookEvent>(b =>
        {
            b.HasKey(e => e.EventId);
            b.Property(e => e.EventId).HasMaxLength(100);
            b.Property(e => e.Provider).HasMaxLength(30);
            b.Property(e => e.Type).HasMaxLength(50);
        });
    }
}
