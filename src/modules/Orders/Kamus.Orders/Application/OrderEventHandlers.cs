using Kamus.Inventory.Contracts;
using Kamus.Orders.Domain;
using Kamus.Orders.Persistence;
using Kamus.Payments.Contracts;
using Kamus.Shared.Auditing;
using Kamus.Shared.Events;
using Kamus.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamus.Orders.Application;

/// <summary>
/// Reage aos eventos de pagamento e estoque. Todos os handlers são idempotentes: o mesmo evento
/// entregue duas vezes não muda o pedido duas vezes (a máquina de estados barra a repetição).
/// O ator vem de quem publicou o evento (ex.: "FakePay", "Expiração da reserva").
/// </summary>
internal sealed class OrderEventHandlers(
    OrdersDbContext db,
    IInventoryService inventory,
    ICurrentActor currentActor,
    TimeProvider clock,
    ILogger<OrderEventHandlers> logger)
    : IEventHandler<PaymentApproved>, IEventHandler<PaymentDeclined>, IEventHandler<ReservationExpired>
{
    public Task HandleAsync(PaymentApproved @event, CancellationToken ct) =>
        ConcurrencyRetry.ExecuteAsync(db, async () =>
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == @event.OrderId, ct);
            if (order is null || order.Status != OrderStatus.AwaitingPayment)
            {
                if (order?.Status is OrderStatus.Cancelled or OrderStatus.PaymentFailed)
                {
                    logger.LogWarning("Pagamento {PaymentId} aprovado para o pedido {Order} já encerrado ({Status}): estorno necessário",
                        @event.PaymentId, order.DisplayNumber, order.Status);
                }

                return false;
            }

            // Converte a reserva em baixa de estoque. Se a reserva já expirou, o estoque pode ter
            // sido vendido para outra pessoa: o pedido é cancelado (e o pagamento, estornado).
            if (await inventory.CommitReservationAsync(order.Id, ct))
            {
                order.MarkPaid(@event.PaymentId, clock.GetUtcNow(), currentActor.Actor);
            }
            else
            {
                order.Cancel("Pagamento aprovado após o prazo da reserva; valor será estornado.", clock.GetUtcNow(), currentActor.Actor);
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pedido {Order} agora está {Status}", order.DisplayNumber, order.Status);
            return true;
        });

    public Task HandleAsync(PaymentDeclined @event, CancellationToken ct) =>
        ConcurrencyRetry.ExecuteAsync(db, async () =>
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == @event.OrderId, ct);
            if (order is not { Status: OrderStatus.AwaitingPayment })
            {
                return false;
            }

            await inventory.ReleaseReservationAsync(order.Id, ct);
            order.MarkPaymentFailed(@event.PaymentId, @event.Reason, clock.GetUtcNow(), currentActor.Actor);
            await db.SaveChangesAsync(ct);
            return true;
        });

    public Task HandleAsync(ReservationExpired @event, CancellationToken ct) =>
        ConcurrencyRetry.ExecuteAsync(db, async () =>
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == @event.OrderId, ct);
            if (order is not { Status: OrderStatus.AwaitingPayment })
            {
                return false;
            }

            order.Cancel("Tempo para pagamento esgotado.", clock.GetUtcNow(), currentActor.Actor);
            await db.SaveChangesAsync(ct);
            return true;
        });
}
