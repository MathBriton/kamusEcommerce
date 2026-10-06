namespace Kamus.Inventory.Domain;

internal enum ReservationStatus
{
    Active,
    Committed,
    Released,
    Expired,
}

/// <summary>Reserva temporária de estoque para um pedido, enquanto o pagamento não é confirmado.</summary>
internal sealed class Reservation
{
    private readonly List<ReservationLineItem> _lines = [];

    private Reservation()
    {
    }

    public Reservation(Guid orderId, IEnumerable<(Guid SkuId, int Quantity)> lines, DateTimeOffset now, TimeSpan timeToLive)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        Status = ReservationStatus.Active;
        CreatedAt = now;
        ExpiresAt = now + timeToLive;
        _lines.AddRange(lines.Select(l => new ReservationLineItem(l.SkuId, l.Quantity)));
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<ReservationLineItem> Lines => _lines;

    public void Close(ReservationStatus status, DateTimeOffset now)
    {
        if (Status != ReservationStatus.Active)
        {
            throw new InvalidOperationException($"Reserva {Id} já está {Status}.");
        }

        Status = status;
        ClosedAt = now;
    }
}

internal sealed record ReservationLineItem(Guid SkuId, int Quantity);
