using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Kamus.Shared.Auditing;

/// <summary>Uma alteração já analisada (ação e diff), aguardando o fim do SaveChanges para virar registro.</summary>
internal sealed class PendingAudit(EntityEntry entry, EntityAuditPolicy policy, string action, IReadOnlyList<AuditChange> changes, int sequence)
{
    public EntityEntry Entry { get; } = entry;

    /// <summary>A instância continua válida depois do SaveChanges, mesmo se a entrada foi desanexada (DELETE).</summary>
    public object Entity { get; } = entry.Entity;

    public EntityAuditPolicy Policy { get; } = policy;

    public string Action { get; } = action;

    public IReadOnlyList<AuditChange> Changes { get; } = changes;

    /// <summary>Ordem em que a entrada apareceu no change tracker (desempate estável).</summary>
    public int Sequence { get; } = sequence;

    public Guid? EntityId { get; set; }

    public AuditSubject? SubjectBeforeSave { get; set; }
}

/// <summary>Regras puras de auditoria sobre uma entrada do change tracker: ação e "antes → depois".</summary>
internal static class AuditAnalyzer
{
    /// <summary>
    /// Ação genérica: Added → created; Deleted → purged (DELETE real); Modified → deleted/restored
    /// quando <see cref="ISoftDeletable.DeletedAt"/> muda de nulo para valor (ou o contrário), senão updated.
    /// </summary>
    public static string? GenericAction(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditActions.Created,
        EntityState.Deleted => AuditActions.Purged,
        EntityState.Modified when entry.Entity is ISoftDeletable => SoftDeleteTransition(entry) ?? AuditActions.Updated,
        EntityState.Modified => AuditActions.Updated,
        _ => null,
    };

    /// <summary>
    /// Ação final: a genérica, refinada pela política quando a genérica é "updated". Retorna
    /// <see langword="null"/> quando a entrada não deve ser auditada (estado sem alteração).
    /// </summary>
    public static string? ResolveAction(EntityEntry entry, EntityAuditPolicy policy)
    {
        var action = GenericAction(entry);
        return action == AuditActions.Updated ? policy.RefineAction(entry) ?? action : action;
    }

    /// <summary>
    /// Diff da allowlist. Added: <c>before</c> nulo (só campos com valor); Deleted (expurgo):
    /// <c>after</c> nulo; Modified: só campos cujo valor formatado mudou.
    /// </summary>
    public static async ValueTask<IReadOnlyList<AuditChange>> ChangesAsync(EntityEntry entry, EntityAuditPolicy policy, CancellationToken ct)
    {
        var changes = new List<AuditChange>();
        foreach (var tracked in policy.Tracked)
        {
            if (tracked.OnlyOnCreate && entry.State != EntityState.Added)
            {
                continue;
            }

            var property = entry.Property(tracked.PropertyName);
            switch (entry.State)
            {
                case EntityState.Added:
                    var created = await tracked.FormatValueAsync(entry.Context, property.CurrentValue, ct);
                    if (created is not null)
                    {
                        changes.Add(new AuditChange(tracked.Label, null, created));
                    }

                    break;

                case EntityState.Deleted:
                    var removed = await tracked.FormatValueAsync(entry.Context, property.OriginalValue, ct);
                    if (removed is not null)
                    {
                        changes.Add(new AuditChange(tracked.Label, removed, null));
                    }

                    break;

                case EntityState.Modified:
                    var (original, current) = (property.OriginalValue, property.CurrentValue);
                    if (property.Metadata.GetValueComparer().Equals(original, current))
                    {
                        continue;
                    }

                    var before = await tracked.FormatValueAsync(entry.Context, original, ct);
                    var after = await tracked.FormatValueAsync(entry.Context, current, ct);
                    if (!string.Equals(before, after, StringComparison.Ordinal))
                    {
                        changes.Add(new AuditChange(tracked.Label, before, after));
                    }

                    break;
            }
        }

        return changes;
    }

    /// <summary>Uma alteração "updated" sem nenhum campo rastreado mudando não gera registro.</summary>
    public static bool ShouldRecord(string action, IReadOnlyList<AuditChange> changes) =>
        action != AuditActions.Updated || changes.Count > 0;

    /// <summary>Chave primária (Guid) da entrada. Auditoria exige chave simples do tipo Guid.</summary>
    public static (Guid Id, bool IsTemporary) ReadKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{entry.Metadata.DisplayName()} não tem chave primária e não pode ser auditada.");
        if (key.Properties is not [var keyProperty])
        {
            throw new InvalidOperationException($"{entry.Metadata.DisplayName()} tem chave composta; a auditoria exige uma chave Guid simples.");
        }

        var property = entry.Property(keyProperty.Name);
        return property.CurrentValue switch
        {
            Guid id => (id, property.IsTemporary),
            _ => throw new InvalidOperationException($"A chave de {entry.Metadata.DisplayName()} não é Guid; a auditoria exige uma chave Guid simples."),
        };
    }

    private static string? SoftDeleteTransition(EntityEntry entry)
    {
        var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAt));
        return (deletedAt.OriginalValue, deletedAt.CurrentValue) switch
        {
            (null, not null) => AuditActions.Deleted,
            (not null, null) => AuditActions.Restored,
            _ => null,
        };
    }
}
