using System.Data.Common;
using Kamus.Audit.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Shared.Auditing;
using Kamus.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kamus.IntegrationTests.Audit;

/// <summary>
/// Entidade de teste com soft delete e auditoria, num schema próprio (<c>audit_probe</c>). Nenhum
/// módulo de negócio tem política ainda nesta etapa, então é ela que exercita os interceptors de
/// ponta a ponta contra o Postgres real.
/// </summary>
internal sealed class Probe : ISoftDeletable
{
    private readonly List<ProbePart> _parts = [];

    private Probe()
    {
    }

    public Probe(string name, string code, decimal price)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Code = code;
        Price = price;
    }

    public Guid Id { get; private set; }

    /// <summary>Gerado pelo banco no insert (como o número do pedido).</summary>
    public int Number { get; private set; }

    public string Name { get; set; } = null!;

    public string Code { get; private set; } = null!;

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }

    public uint Version { get; private set; }

    public IReadOnlyList<ProbePart> Parts => _parts;

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public string? DeletedByName { get; private set; }

    public ProbePart AddPart(string color)
    {
        var part = new ProbePart(Id, color);
        _parts.Add(part);
        return part;
    }
}

internal sealed class ProbePart : ISoftDeletable
{
    private ProbePart()
    {
    }

    public ProbePart(Guid probeId, string color) => (Id, ProbeId, Color) = (Guid.CreateVersion7(), probeId, color);

    public Guid Id { get; private set; }

    public Guid ProbeId { get; private set; }

    public string Color { get; set; } = null!;

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public string? DeletedByName { get; private set; }
}

internal sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
{
    public const string Schema = "audit_probe";

    public DbSet<Probe> Probes => Set<Probe>();

    public DbSet<ProbePart> Parts => Set<ProbePart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasSequence<int>("probe_numbers").StartsAt(1000);

        modelBuilder.Entity<Probe>(b =>
        {
            b.HasSoftDelete();
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.Number).HasDefaultValueSql("nextval('audit_probe.probe_numbers')").ValueGeneratedOnAdd();
            b.Property(p => p.Version).IsRowVersion();
            b.Property(p => p.Price).HasPrecision(10, 2);
            b.HasIndex(p => p.Code).IsUnique().HasFilter("deleted_at IS NULL");
            b.HasMany(p => p.Parts).WithOne().HasForeignKey(p => p.ProbeId);
            b.Navigation(p => p.Parts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProbePart>(b =>
        {
            b.HasSoftDelete();
            b.Property(p => p.Id).ValueGeneratedNever();
        });
    }
}

/// <summary><see cref="IAuditLog"/> real, com uma chave para simular falha na gravação da auditoria.</summary>
internal sealed class SwitchableAuditLog(PostgresAuditLog inner) : IAuditLog
{
    public bool Fail { get; set; }

    public Task WriteAsync(IReadOnlyList<AuditRecord> records, DbConnection connection, DbTransaction transaction, CancellationToken ct) =>
        Fail ? throw new InvalidOperationException("Falha simulada na auditoria.") : inner.WriteAsync(records, connection, transaction, ct);
}

/// <summary>Container de DI próprio (fora da API) com o <see cref="ProbeDbContext"/> auditado.</summary>
internal sealed class ProbeHost
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static ProbeHost? _instance;

    private ProbeHost(ServiceProvider services, SwitchableAuditLog log) => (Services, Log) = (services, log);

    public ServiceProvider Services { get; }

    public SwitchableAuditLog Log { get; }

    public static async Task<ProbeHost> GetAsync(KamusApiFactory factory)
    {
        await Gate.WaitAsync();
        try
        {
            if (_instance is not null)
            {
                return _instance;
            }

            _ = factory.Server; // garante as migrations (inclusive audit.entries)

            var log = new SwitchableAuditLog(new PostgresAuditLog());
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new NpgsqlDataSourceBuilder(factory.PostgresConnectionString).Build());
            services.AddSingleton(TimeProvider.System);
            services.AddCurrentActor();
            services.AddSingleton<IAuditLog>(log);
            services.AddModuleDbContext<AuditDbContext>(AuditDbContext.Schema);
            services.AddModuleDbContext<ProbeDbContext>(ProbeDbContext.Schema, audit => audit
                .Module("probe")
                .Entity<Probe>(e => e
                    .Subject("Probe", p => p.Id, p => $"Probe #{p.Number}")
                    .Detail(p => p.Code)
                    .Track(p => p.Name, "Nome")
                    .Track(p => p.Price, "Preço")
                    .Track(p => p.IsActive, "Situação", v => v is true ? "Publicado" : "Rascunho")
                    .Action(entry => entry.HasChanged(p => p.IsActive) ? (entry.Entity.IsActive ? "published" : "unpublished") : null))
                .Entity<ProbePart>(e => e
                    .SubjectAsync("Probe", async (sp, part, ct) =>
                    {
                        // Antes de gravar: o pai está no change tracker ou ainda no banco (mesmo num expurgo).
                        var db = sp.GetRequiredService<ProbeDbContext>();
                        var name = db.ChangeTracker.Entries<Probe>().FirstOrDefault(p => p.Entity.Id == part.ProbeId)?.Entity.Name
                            ?? await db.Probes.IgnoreQueryFilters().AsNoTracking()
                                .Where(p => p.Id == part.ProbeId).Select(p => p.Name).FirstOrDefaultAsync(ct);
                        return (part.ProbeId, name, $"Cor {part.Color}");
                    })
                    .Track(p => p.Color, "Cor")));

            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
                await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            }

            return _instance = new ProbeHost(provider, log);
        }
        finally
        {
            Gate.Release();
        }
    }
}
