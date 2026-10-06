using Kamus.Payments.Contracts;
using Kamus.Payments.Domain;
using Kamus.Payments.Persistence;
using Kamus.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kamus.Payments.Application;

/// <summary>
/// Avisa o módulo Orders sobre o resultado de um pagamento e marca o aviso como entregue.
/// Se o aviso falhar, o <see cref="PaymentNotificationDispatcher"/> tenta de novo depois
/// (um "outbox" simplificado; a R3 formaliza o padrão).
/// </summary>
internal sealed class PaymentResultNotifier(
    PaymentsDbContext db,
    IEventPublisher events,
    TimeProvider clock,
    ILogger<PaymentResultNotifier> logger)
{
    public async Task NotifyAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            if (payment.Status == PaymentStatus.Approved)
            {
                await events.PublishAsync(new PaymentApproved(payment.OrderId, payment.Id), ct);
            }
            else
            {
                await events.PublishAsync(new PaymentDeclined(payment.OrderId, payment.Id, payment.FailureReason ?? "Pagamento recusado"), ct);
            }

            payment.MarkOrderNotified(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao avisar o pedido {OrderId} sobre o pagamento {PaymentId}; será reenviado", payment.OrderId, payment.Id);
        }
    }

    public async Task<int> RetryPendingAsync(TimeSpan minAge, CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow() - minAge;
        var pending = await db.Payments
            .Where(p => p.Status != PaymentStatus.Pending && p.OrderNotifiedAt == null && p.UpdatedAt <= cutoff)
            .OrderBy(p => p.UpdatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var payment in pending)
        {
            await NotifyAsync(payment, ct);
        }

        return pending.Count;
    }
}

internal sealed class PaymentNotificationDispatcher(IServiceScopeFactory scopes, ILogger<PaymentNotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<PaymentResultNotifier>();
                await notifier.RetryPendingAsync(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao reenviar avisos de pagamento");
            }
        }
    }
}
