namespace Kamus.Payments.Domain;

internal enum PaymentStatus
{
    Pending,
    Approved,
    Declined,
}

internal sealed class Payment
{
    private Payment()
    {
    }

    public Payment(Guid orderId, string orderNumber, decimal amount, string cardLast4, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        OrderNumber = orderNumber;
        Amount = amount;
        CardLast4 = cardLast4;
        Provider = "fakepay";
        Status = PaymentStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string OrderNumber { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public string CardLast4 { get; private set; } = null!;

    public string Provider { get; private set; } = null!;

    public string? ProviderChargeId { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Quando o módulo Orders foi avisado do resultado. Nulo = aviso pendente (será reenviado).</summary>
    public DateTimeOffset? OrderNotifiedAt { get; private set; }

    public uint Version { get; private set; }

    public bool IsFinal => Status != PaymentStatus.Pending;

    public void AttachCharge(string chargeId) => ProviderChargeId = chargeId;

    /// <summary>Aplica o resultado do provedor. Um pagamento já finalizado não muda mais.</summary>
    public bool Resolve(bool approved, string? failureReason, DateTimeOffset now)
    {
        if (IsFinal)
        {
            return false;
        }

        Status = approved ? PaymentStatus.Approved : PaymentStatus.Declined;
        FailureReason = approved ? null : failureReason ?? "Pagamento recusado";
        UpdatedAt = now;
        return true;
    }

    public void MarkOrderNotified(DateTimeOffset now) => OrderNotifiedAt = now;
}

/// <summary>Registro de webhook já processado: a chave primária é o id do evento do provedor.</summary>
internal sealed class ProcessedWebhookEvent
{
    private ProcessedWebhookEvent()
    {
    }

    public ProcessedWebhookEvent(string eventId, string provider, string type, DateTimeOffset processedAt)
    {
        EventId = eventId;
        Provider = provider;
        Type = type;
        ProcessedAt = processedAt;
    }

    public string EventId { get; private set; } = null!;

    public string Provider { get; private set; } = null!;

    public string Type { get; private set; } = null!;

    public DateTimeOffset ProcessedAt { get; private set; }
}
