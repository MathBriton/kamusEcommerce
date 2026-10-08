using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.UnitTests.Auditing;

/// <summary>
/// Modelo de teste: o change tracker do EF funciona sem banco (a conexão nunca é aberta), então as
/// regras de auditoria e de soft delete são testadas sobre entradas reais, sem provider em memória.
/// </summary>
internal sealed class Gadget : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = "Camisa de Linho";

    public decimal Price { get; set; } = 249.90m;

    public bool IsActive { get; set; }

    public string Code { get; set; } = "KM001";

    public DateTimeOffset UpdatedAt { get; set; }

    public List<GadgetPart> Parts { get; } = [];

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public string? DeletedByName { get; private set; }

    public void MarkDeleted(DateTimeOffset at) => (DeletedAt, DeletedByName) = (at, "Alguém");
}

internal sealed class GadgetPart : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid GadgetId { get; set; }

    public string Color { get; set; } = "Areia";

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public string? DeletedByName { get; private set; }

    public void MarkDeleted(DateTimeOffset at) => (DeletedAt, DeletedByName) = (at, "Alguém");
}

internal sealed class OfflineDbContext() : DbContext(new DbContextOptionsBuilder<OfflineDbContext>()
    .UseNpgsql("Host=offline.invalid;Database=kamus")
    .Options)
{
    public DbSet<Gadget> Gadgets => Set<Gadget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Gadget>(b =>
        {
            b.Property(g => g.Id).ValueGeneratedNever();
            b.HasMany(g => g.Parts).WithOne().HasForeignKey(p => p.GadgetId);
        });
        modelBuilder.Entity<GadgetPart>().Property(p => p.Id).ValueGeneratedNever();
    }

    /// <summary>Entidade como se tivesse vindo do banco (Unchanged, valores originais = atuais).</summary>
    public Gadget Loaded(Gadget gadget)
    {
        Attach(gadget);
        return gadget;
    }
}
