using Kamus.Inventory.Contracts;
using Kamus.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Inventory.Application;

internal sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>Validade da reserva de estoque criada ao iniciar o pagamento.</summary>
    public TimeSpan ReservationTimeToLive { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan ExpiryCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Varre periodicamente as reservas vencidas, devolve o estoque e avisa o módulo Orders.</summary>
internal sealed class ReservationExpiryWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<InventoryOptions> options,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ExpiryCheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(clock.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao expirar reservas");
            }
        }
    }

    public async Task RunOnceAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();
        var events = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        foreach (var orderId in await inventory.ExpireDueReservationsAsync(now, cancellationToken))
        {
            logger.LogInformation("Reserva do pedido {OrderId} expirou", orderId);
            await events.PublishAsync(new ReservationExpired(orderId), cancellationToken);
        }
    }
}
