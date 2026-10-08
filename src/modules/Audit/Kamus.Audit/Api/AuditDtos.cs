using Kamus.Shared.Auditing;

namespace Kamus.Audit.Api;

/// <summary>Quem fez a alteração. <c>Kind</c>: "Admin", "Customer" ou "System".</summary>
public sealed record AuditActorDto(string Kind, Guid? Id, string Name, string? Email);

public sealed record AuditEntryDto(
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
    AuditActorDto Actor,
    string? CorrelationId,
    string? IpAddress);

public sealed record AuditEntriesPage(IReadOnlyList<AuditEntryDto> Items, int Total, int Page, int PageSize);

/// <summary>Opção do filtro "Usuário". <c>Key</c> é o id do usuário ou <c>system:&lt;nome&gt;</c>.</summary>
public sealed record AuditActorOptionDto(string Key, string Kind, string Name, string? Email);
