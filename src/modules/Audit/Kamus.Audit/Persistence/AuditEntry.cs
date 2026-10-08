namespace Kamus.Audit.Persistence;

/// <summary>
/// Linha de <c>audit.entries</c>, só para leitura: quem grava é o <see cref="PostgresAuditLog"/>
/// (SQL direto, na transação de quem alterou) e o banco recusa UPDATE/DELETE/TRUNCATE.
/// </summary>
internal sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Module { get; private set; } = null!;

    public string EntityType { get; private set; } = null!;

    public Guid EntityId { get; private set; }

    public string Action { get; private set; } = null!;

    public string SubjectType { get; private set; } = null!;

    public Guid SubjectId { get; private set; }

    public string? SubjectLabel { get; private set; }

    public string? Detail { get; private set; }

    /// <summary>JSON (<c>jsonb</c>): <c>[{ "field", "before", "after" }]</c>.</summary>
    public string Changes { get; private set; } = null!;

    public string ActorKind { get; private set; } = null!;

    public Guid? ActorId { get; private set; }

    public string ActorName { get; private set; } = null!;

    public string? ActorEmail { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? IpAddress { get; private set; }
}
