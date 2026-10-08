using Kamus.Orders.Domain;
using Kamus.Shared.Auditing;

namespace Kamus.Orders.Persistence;

/// <summary>
/// Política de auditoria do módulo Orders: cada mudança de situação do pedido vira um registro, com
/// quem fez (cliente, loja ou sistema) e o "antes → depois" da situação e do rastreio.
/// </summary>
internal static class OrdersAudit
{
    public const string Module = "orders";

    public const string SubjectType = "Order";

    public static void Configure(AuditPolicyBuilder audit) => audit
        .Module(Module)
        .Entity<Order>(e => e
            // O número do pedido é gerado pelo banco no insert: o rótulo é montado depois de gravar.
            .Subject(SubjectType, o => o.Id, o => $"Pedido {o.DisplayNumber}")
            .Detail(o => o.Address.RecipientName)
            .Track(o => o.Status, "Situação", v => v is OrderStatus status ? StatusLabel(status) : null)
            .Track(o => o.TrackingCode, "Rastreio")
            .Action(entry => entry.HasChanged(o => o.Status) ? ActionFor(entry.Entity.Status) : null));

    /// <summary>Situação como aparece nas telas (e no "antes → depois").</summary>
    public static string StatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Created => "Criado",
        OrderStatus.AwaitingPayment => "Aguardando pagamento",
        OrderStatus.Paid => "Pago",
        OrderStatus.Shipped => "Enviado",
        OrderStatus.Delivered => "Entregue",
        OrderStatus.Cancelled => "Cancelado",
        OrderStatus.PaymentFailed => "Pagamento recusado",
        _ => status.ToString(),
    };

    /// <summary>
    /// Ação refinada pela situação nova. <see langword="null"/> mantém "updated" (ex.: só o rastreio
    /// mudou); a criação já é "created" e não passa por aqui.
    /// </summary>
    public static string? ActionFor(OrderStatus status) => status switch
    {
        OrderStatus.AwaitingPayment => "payment_started",
        OrderStatus.Paid => "paid",
        OrderStatus.PaymentFailed => "payment_failed",
        OrderStatus.Shipped => "shipped",
        OrderStatus.Delivered => "delivered",
        OrderStatus.Cancelled => "cancelled",
        _ => null,
    };
}
