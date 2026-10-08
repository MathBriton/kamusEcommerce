using Kamus.Shared.Auditing;
using Kamus.Shared.Results;

namespace Kamus.Orders.Domain;

/// <summary>
/// Pedido. Guarda um snapshot de itens, preços e endereço: depois de criado, não depende do
/// catálogo atual (mudanças de preço ou produtos removidos não alteram pedidos antigos).
/// </summary>
/// <remarks>
/// A criação e as transições recebem o ator (cliente, loja ou sistema): o histórico diz quem fez
/// cada mudança. Pedidos nunca são excluídos.
/// </remarks>
internal sealed class Order
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusChange> _history = [];

    private Order()
    {
    }

    private Order(Guid customerId, IEnumerable<OrderItem> items, ShippingAddress address, ShippingInfo shipping, DateTimeOffset now, AuditActor actor)
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
        _history.Add(OrderStatusChange.By(actor, OrderStatus.Created, now, null));
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

    /// <summary>Código de rastreio informado no despacho.</summary>
    public string? TrackingCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Mudanças de situação em ordem cronológica. O banco não garante a ordem das linhas ao carregar a
    /// coleção, então a ordem vem de <see cref="OrderStatusChange.At"/>; no empate (criação e início do
    /// pagamento têm o mesmo instante), vale a progressão da máquina de estados.
    /// </summary>
    public IReadOnlyList<OrderStatusChange> History => [.. _history.OrderBy(h => h.At).ThenBy(h => h.Status)];

    public uint Version { get; private set; }

    /// <param name="actor">Quem criou o pedido (o cliente): vai para o histórico.</param>
    public static Order Create(Guid customerId, IReadOnlyCollection<OrderItem> items, ShippingAddress address, ShippingInfo shipping, DateTimeOffset now, AuditActor actor)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Pedido sem itens.", nameof(items));
        }

        ArgumentNullException.ThrowIfNull(actor);
        return new Order(customerId, items, address, shipping, now, actor);
    }

    public Result StartPayment(DateTimeOffset now, AuditActor actor) => TransitionTo(OrderStatus.AwaitingPayment, now, actor);

    public Result MarkPaid(Guid paymentId, DateTimeOffset now, AuditActor actor)
    {
        var result = TransitionTo(OrderStatus.Paid, now, actor);
        if (result.IsSuccess)
        {
            PaymentId = paymentId;
        }

        return result;
    }

    public Result MarkPaymentFailed(Guid paymentId, string reason, DateTimeOffset now, AuditActor actor)
    {
        var result = TransitionTo(OrderStatus.PaymentFailed, now, actor, reason);
        if (result.IsSuccess)
        {
            PaymentId = paymentId;
        }

        return result;
    }

    public Result Cancel(string reason, DateTimeOffset now, AuditActor actor) => TransitionTo(OrderStatus.Cancelled, now, actor, reason);

    public Result Ship(DateTimeOffset now, AuditActor actor, string? trackingCode = null)
    {
        var result = TransitionTo(OrderStatus.Shipped, now, actor, trackingCode is null ? null : $"Rastreio: {trackingCode}");
        if (result.IsSuccess)
        {
            TrackingCode = trackingCode;
        }

        return result;
    }

    public Result Deliver(DateTimeOffset now, AuditActor actor) => TransitionTo(OrderStatus.Delivered, now, actor);

    private Result TransitionTo(OrderStatus next, DateTimeOffset now, AuditActor actor, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!OrderStateMachine.CanTransition(Status, next))
        {
            return Error.Conflict("orders.invalid_transition", $"Pedido {DisplayNumber} não pode ir de {Status} para {next}.");
        }

        Status = next;
        UpdatedAt = now;
        _history.Add(OrderStatusChange.By(actor, next, now, note));
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

/// <summary>
/// Uma mudança de situação do pedido. <see cref="ActorKind"/> ("Admin", "Customer" ou "System") e
/// <see cref="ActorName"/> dizem quem a fez; são nulos em pedidos anteriores à auditoria (R12).
/// </summary>
internal sealed record OrderStatusChange(OrderStatus Status, DateTimeOffset At, string? Note, string? ActorKind = null, string? ActorName = null)
{
    /// <summary>Tamanho da coluna <c>actor_name</c> (igual ao nome do ator na auditoria).</summary>
    public const int ActorNameMaxLength = 150;

    public static OrderStatusChange By(AuditActor actor, OrderStatus status, DateTimeOffset at, string? note)
    {
        var name = actor.Name.Length > ActorNameMaxLength ? actor.Name[..ActorNameMaxLength] : actor.Name;
        return new OrderStatusChange(status, at, note, actor.Kind.ToString(), name);
    }
}
