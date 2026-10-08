using Kamus.Audit.Persistence;
using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Audit.Application;

internal sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Piso de segurança: o banco (trigger) também recusa apagar registros mais novos que isso.</summary>
    public const int MinimumRetentionDays = 365;

    /// <summary>Por quanto tempo os registros são guardados. Padrão: 2 anos.</summary>
    public int RetentionDays { get; set; } = 730;

    /// <summary>Intervalo entre as limpezas.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>Uma vez por dia, apaga os registros de auditoria mais antigos que a retenção configurada.</summary>
internal sealed class AuditRetentionWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<AuditOptions> options,
    ILogger<AuditRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao aplicar a retenção da auditoria");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Apaga o que passou da retenção. Retorna quantos registros foram apagados.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        var retention = Math.Max(options.Value.RetentionDays, AuditOptions.MinimumRetentionDays);
        var cutoff = clock.GetUtcNow().AddDays(-retention);

        using (actor.ActAs(AuditActor.System("Retenção da auditoria")))
        {
            // A segunda condição repete o piso do trigger com o relógio do banco: mesmo com relógios
            // diferentes entre app e banco, o DELETE nunca esbarra na proteção.
            var deleted = await db.Database.ExecuteSqlAsync(
                $"DELETE FROM audit.entries WHERE occurred_at < {cutoff} AND occurred_at < now() - interval '365 days'",
                cancellationToken);

            if (deleted > 0)
            {
                logger.LogInformation("{Actor}: {Count} registros de auditoria anteriores a {Cutoff:yyyy-MM-dd} apagados",
                    actor.Actor.Name, deleted, cutoff);
            }

            return deleted;
        }
    }
}
