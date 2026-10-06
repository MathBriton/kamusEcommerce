namespace Kamus.Orders.Domain;

public enum OrderStatus
{
    Created,
    AwaitingPayment,
    Paid,
    Shipped,
    Delivered,
    Cancelled,
    PaymentFailed,
}

/// <summary>
/// Máquina de estados do pedido:
/// Created → AwaitingPayment → Paid → Shipped → Delivered, com Cancelled e PaymentFailed como saídas.
/// </summary>
public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.Created] = [OrderStatus.AwaitingPayment, OrderStatus.Cancelled],
        [OrderStatus.AwaitingPayment] = [OrderStatus.Paid, OrderStatus.PaymentFailed, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Shipped],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.PaymentFailed] = [],
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) => Transitions[from].Contains(to);

    public static IReadOnlyList<OrderStatus> NextStates(OrderStatus from) => Transitions[from];

    public static bool IsTerminal(OrderStatus status) => Transitions[status].Length == 0;
}
