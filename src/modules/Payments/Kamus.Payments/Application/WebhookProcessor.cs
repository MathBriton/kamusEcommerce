using Kamus.Payments.Domain;
using Kamus.Payments.FakePay;
using Kamus.Payments.Persistence;
using Kamus.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamus.Payments.Application;

internal enum WebhookOutcome
{
    Processed,
    Duplicate,
    UnknownPayment,
}

/// <summary>
/// Processa webhooks com idempotência: o id do evento é gravado na MESMA transação que altera o
/// pagamento. A chave primária garante que, mesmo com entregas simultâneas, só uma passa.
/// </summary>
internal sealed class WebhookProcessor(
    PaymentsDbContext db,
    PaymentResultNotifier notifier,
    TimeProvider clock,
    ILogger<WebhookProcessor> logger)
{
    public async Task<WebhookOutcome> ProcessAsync(FakePayWebhookEvent @event, CancellationToken ct)
    {
        if (await db.ProcessedWebhookEvents.AnyAsync(e => e.EventId == @event.Id, ct))
        {
            logger.LogInformation("Webhook {EventId} duplicado ignorado", @event.Id);
            return WebhookOutcome.Duplicate;
        }

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == @event.Data.Reference, ct);
        if (payment is null)
        {
            return WebhookOutcome.UnknownPayment;
        }

        var now = clock.GetUtcNow();
        db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent(@event.Id, payment.Provider, @event.Type, now));
        var changed = @event.Type switch
        {
            FakePayWebhookEvent.ChargeSucceeded => payment.Resolve(approved: true, null, now),
            FakePayWebhookEvent.ChargeFailed => payment.Resolve(approved: false, @event.Data.FailureReason, now),
            _ => false,
        };

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation() || ex is DbUpdateConcurrencyException)
        {
            // Outra entrega do mesmo evento (ou de outro evento do mesmo pagamento) ganhou a corrida.
            logger.LogInformation("Webhook {EventId} processado em paralelo por outra requisição", @event.Id);
            return WebhookOutcome.Duplicate;
        }

        if (changed)
        {
            await notifier.NotifyAsync(payment, ct);
        }

        return WebhookOutcome.Processed;
    }
}
