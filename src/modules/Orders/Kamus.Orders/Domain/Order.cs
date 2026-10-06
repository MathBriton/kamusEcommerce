using Kamus.Shared.Results;

namespace Kamus.Orders.Domain;

/// <summary>
/// Pedido. Guarda um snapshot de itens, preços e endereço: depois de criado, não depende do
/// catálogo atual (mudanças de preço ou produtos removidos não alteram pedidos antigos).
/// </summary>
internal sealed class Order
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusChange> _history = [];

    private Order()
    {
    }

    private Order(Guid customerId, IEnumerable<OrderItem> items, ShippingAddress address, ShippingInfo shipping, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        CustomerId = customerId;
        _items.AddRange(items);
        Address = address;
        Shipping = shipping;
        Subtotal = _items.Sum(i => i.LineTotal);
        Total = Subtotal + shipping.Cost;
        CreatedAt = now;
        UpdatedAt = now;
        Status = OrderStatus.Created;
        _history.Add(new OrderStatusChange(OrderStatus.Created, now, null));
    }

    public Guid Id { get; private set; }

    /// <summary>Número sequencial gerado pelo banco; exibido como "KM10001".</summary>
    public long Number { get; private set; }

    public string DisplayNumber => $"KM{Number}";

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public ShippingAddress Address { get; private set; } = null!;

    public ShippingInfo Shipping { get; private set; } = null!;

    public decimal Subtotal { get; private set; }

    public decimal Total { get; private set; }

    public Guid? PaymentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<OrderStatusChange> History => _history;

    public uint Version { get; private set; }

    public static Order Create(Guid customerId, IReadOnlyCollection<OrderItem> items, ShippingAddress address, ShippingInfo shipping, DateTimeOffset now)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Pedido sem itens.", nameof(items));
        }

        return new Order(customerId, items, address, shipping, now);
    }

    public Result StartPayment(DateTimeOffset now) => TransitionTo(OrderStatus.AwaitingPayment, now);

    public Result MarkPaid(Guid paymentId, DateTimeOffset now)
    {
        var result = TransitionTo(OrderStatus.Paid, now);
        if (result.IsSuccess)
        {
            PaymentId = paymentId;
        }

        return result;
    }

    public Result MarkPaymentFailed(Guid paymentId, string reason, DateTimeOffset now)
    {
        var result = TransitionTo(OrderStatus.PaymentFailed, now, reason);
        if (result.IsSuccess)
        {
            PaymentId = paymentId;
        }

        return result;
    }

    public Result Cancel(string reason, DateTimeOffset now) => TransitionTo(OrderStatus.Cancelled, now, reason);

    public Result Ship(DateTimeOffset now) => TransitionTo(OrderStatus.Shipped, now);

    public Result Deliver(DateTimeOffset now) => TransitionTo(OrderStatus.Delivered, now);

    private Result TransitionTo(OrderStatus next, DateTimeOffset now, string? note = null)
    {
        if (!OrderStateMachine.CanTransition(Status, next))
        {
            return Error.Conflict("orders.invalid_transition", $"Pedido {DisplayNumber} não pode ir de {Status} para {next}.");
        }

        Status = next;
        UpdatedAt = now;
        _history.Add(new OrderStatusChange(next, now, note));
        return Result.Success();
    }
}

internal sealed record OrderItem(
    Guid SkuId,
    string SkuCode,
    string ProductName,
    string ProductPath,
    string Color,
    string Size,
    string? ImageUrl,
    decimal UnitPrice,
    decimal ListPrice,
    int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

internal sealed record ShippingAddress(
    string RecipientName,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State);

internal sealed record ShippingInfo(string Region, decimal Cost, int EstimatedDays);

internal sealed record OrderStatusChange(OrderStatus Status, DateTimeOffset At, string? Note);
