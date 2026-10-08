using Microsoft.EntityFrameworkCore;

namespace Kamus.Audit.Persistence;

/// <summary>Migrations e consultas de <c>audit.entries</c>. Não grava: a tabela é somente inclusão.</summary>
internal sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public const string Schema = "audit";

    public DbSet<AuditEntry> Entries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("entries");
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.Module).HasMaxLength(AuditColumns.Module);
            b.Property(e => e.EntityType).HasMaxLength(AuditColumns.EntityType);
            b.Property(e => e.Action).HasMaxLength(AuditColumns.Action);
            b.Property(e => e.SubjectType).HasMaxLength(AuditColumns.SubjectType);
            b.Property(e => e.SubjectLabel).HasMaxLength(AuditColumns.SubjectLabel);
            b.Property(e => e.Detail).HasMaxLength(AuditColumns.Detail);
            b.Property(e => e.Changes).HasColumnType("jsonb");
            b.Property(e => e.ActorKind).HasMaxLength(AuditColumns.ActorKind);
            b.Property(e => e.ActorName).HasMaxLength(AuditColumns.ActorName);
            b.Property(e => e.ActorEmail).HasMaxLength(AuditColumns.ActorEmail);
            b.Property(e => e.CorrelationId).HasMaxLength(AuditColumns.CorrelationId);
            b.Property(e => e.IpAddress).HasMaxLength(AuditColumns.IpAddress);

            b.HasIndex(e => new { e.OccurredAt, e.Id });
            b.HasIndex(e => new { e.SubjectType, e.SubjectId, e.OccurredAt });
            b.HasIndex(e => new { e.ActorId, e.OccurredAt });
            b.HasIndex(e => new { e.Module, e.OccurredAt });
            b.HasIndex(e => e.Action);
        });
    }
}

/// <summary>Tamanhos das colunas de <c>audit.entries</c> (o <see cref="PostgresAuditLog"/> corta o que passar).</summary>
internal static class AuditColumns
{
    public const int Module = 30;
    public const int EntityType = 40;
    public const int Action = 40;
    public const int SubjectType = 40;
    public const int SubjectLabel = 300;
    public const int Detail = 300;
    public const int ActorKind = 20;
    public const int ActorName = 150;
    public const int ActorEmail = 256;
    public const int CorrelationId = 100;
    public const int IpAddress = 64;
}
