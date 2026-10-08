using System.Data.Common;

namespace Kamus.Shared.Auditing;

/// <summary>Um campo que mudou: valores já formatados para exibição. Em inclusões, <c>Before</c> é nulo; em exclusões definitivas, <c>After</c>.</summary>
public sealed record AuditChange(string Field, string? Before, string? After);

/// <summary>Registro imutável de uma alteração, gravado pelo <see cref="IAuditLog"/>.</summary>
/// <param name="Id">Guid v7 baseado em <paramref name="OccurredAt"/>.</param>
/// <param name="Module">Módulo dono da entidade: "catalog", "inventory", "orders".</param>
/// <param name="EntityType">Entidade alterada: "Product", "Sku", "ProductImage", "StockLevel", "Order".</param>
/// <param name="Action">Código canônico (<see cref="AuditActions"/> ou um refinado pela política).</param>
/// <param name="SubjectType">Agregado exibido nas telas: "Product", "Order".</param>
/// <param name="SubjectLabel">Rótulo do agregado: "Camisa de Linho Areia", "Pedido KM10003".</param>
/// <param name="Detail">Complemento: "Areia · P", "Cor Areia", "Maria Silva".</param>
/// <param name="Changes">Só campos da allowlist da política que mudaram.</param>
public sealed record AuditRecord(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Module,
    string EntityType,
    Guid EntityId,
    string Action,
    string SubjectType,
    Guid SubjectId,
    string? SubjectLabel,
    string? Detail,
    IReadOnlyList<AuditChange> Changes,
    AuditActor Actor,
    string? CorrelationId,
    string? IpAddress);

/// <summary>
/// Grava registros de auditoria. Implementado pelo módulo Audit; como <c>IEventPublisher</c>, o
/// contrato é building block do Shared para que qualquer módulo audite sem depender do Audit.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Grava <paramref name="records"/> NA MESMA conexão e transação da alteração auditada: ou os dois
    /// são persistidos, ou nenhum (atomicidade).
    /// </summary>
    Task WriteAsync(IReadOnlyList<AuditRecord> records, DbConnection connection, DbTransaction transaction, CancellationToken ct);
}

/// <summary>Códigos de ação calculados pelo interceptor. Ações refinadas (ex.: "published") vêm da política de cada módulo.</summary>
public static class AuditActions
{
    public const string Created = "created";

    public const string Updated = "updated";

    /// <summary>Soft delete: <c>DeletedAt</c> passou de nulo para um valor.</summary>
    public const string Deleted = "deleted";

    /// <summary>Saiu da lixeira: <c>DeletedAt</c> voltou a nulo.</summary>
    public const string Restored = "restored";

    /// <summary>Exclusão definitiva (DELETE real).</summary>
    public const string Purged = "purged";
}
