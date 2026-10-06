using Kamus.Payments.Contracts;
using Kamus.Payments.Domain;
using Kamus.Payments.FakePay;
using Kamus.Payments.Persistence;

namespace Kamus.Payments.Application;

internal sealed class PaymentService(PaymentsDbContext db, IFakePayApi fakePay, TimeProvider clock) : IPaymentService
{
    public async Task<PaymentStarted> StartAsync(StartPayment request, CancellationToken cancellationToken = default)
    {
        var card = CardNumberRules.Normalize(request.CardNumber);
        var payment = new Payment(request.OrderId, request.OrderNumber, request.Amount, card[^4..], clock.GetUtcNow());
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        // O número do cartão vai só para o provedor; a loja guarda apenas os 4 últimos dígitos.
        var chargeId = await fakePay.CreateChargeAsync(payment.Id, payment.Amount, card, cancellationToken);
        payment.AttachCharge(chargeId);
        await db.SaveChangesAsync(cancellationToken);

        return new PaymentStarted(payment.Id);
    }
}
