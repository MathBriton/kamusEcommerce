using Kamus.Shared.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Catalog.Application;

/// <summary>
/// Expurgo automático da lixeira do catálogo: a cada <see cref="CatalogOptions.TrashPurgeInterval"/>
/// (padrão 6 h), apaga de vez o que está lá há mais de <see cref="CatalogOptions.TrashRetentionDays"/>
/// dias. Na auditoria, o ator é o sistema ("Expurgo automático").
/// </summary>
internal sealed class TrashPurgeWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<CatalogOptions> options,
    ILogger<TrashPurgeWorker> logger) : BackgroundService
{
    public static readonly AuditActor Actor = AuditActor.System("Expurgo automático");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.TrashPurgeInterval, clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha no expurgo automático da lixeira do catálogo");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Uma rodada do expurgo. Retorna quantos itens foram apagados de vez.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
        var trash = scope.ServiceProvider.GetRequiredService<CatalogTrashService>();
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.TrashRetentionDays);

        using (actor.ActAs(Actor))
        {
            var purged = await trash.PurgeExpiredAsync(cutoff, cancellationToken);
            if (purged > 0)
            {
                logger.LogInformation("Expurgo automático: {Count} itens da lixeira do catálogo excluídos antes de {Cutoff:yyyy-MM-dd} foram apagados de vez",
                    purged, cutoff);
            }

            return purged;
        }
    }
}
