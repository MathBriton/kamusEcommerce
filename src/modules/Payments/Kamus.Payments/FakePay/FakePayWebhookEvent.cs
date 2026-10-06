namespace Kamus.Payments.FakePay;

/// <summary>Corpo dos webhooks do FakePay.</summary>
internal sealed record FakePayWebhookEvent(string Id, string Type, long Created, FakePayChargeData Data)
{
    public const string ChargeSucceeded = "charge.succeeded";
    public const string ChargeFailed = "charge.failed";
}

/// <param name="Reference">Identificador que a loja enviou ao criar a cobrança (o id do pagamento).</param>
internal sealed record FakePayChargeData(string ChargeId, Guid Reference, decimal Amount, string? FailureReason);
