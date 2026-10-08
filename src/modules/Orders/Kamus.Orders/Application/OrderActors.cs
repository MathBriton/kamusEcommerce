using Kamus.Shared.Auditing;

namespace Kamus.Orders.Application;

/// <summary>Quem aparece no histórico do pedido (e na auditoria) para cada tipo de operação.</summary>
internal static class OrderActors
{
    /// <summary>Envio e entrega simulados pela vitrine (só em desenvolvimento e testes).</summary>
    public static readonly AuditActor FulfillmentSimulation = AuditActor.System("Simulação de entrega");

    /// <summary>
    /// Ator de quem compra ou cancela pela loja: sempre cliente, mesmo que a conta também tenha o
    /// papel Admin (ali ela está comprando, não operando o backoffice).
    /// </summary>
    public static AuditActor Customer(this ICurrentActor current) => current.Actor switch
    {
        { UserId: not null, Kind: not AuditActorKind.Customer } actor => actor with { Kind = AuditActorKind.Customer },
        var actor => actor,
    };
}
