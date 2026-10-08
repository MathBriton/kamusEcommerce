using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Kamus.Shared.Auditing;

/// <summary>
/// Transforma <c>db.Remove(...)</c> de entidades <see cref="ISoftDeletable"/> em exclusão lógica.
/// Registrado antes do <see cref="AuditingInterceptor"/>, que então enxerga "deleted" (UPDATE) ou
/// "purged" (DELETE real).
/// </summary>
internal sealed class SoftDeleteInterceptor(ICurrentActor currentActor, TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is not null)
        {
            SoftDeleteRules.Apply(context, currentActor.Actor, clock.GetUtcNow());
        }
    }
}

/// <summary>Regras do soft delete, separadas do interceptor para serem testadas sem banco.</summary>
internal static class SoftDeleteRules
{
    internal enum Decision
    {
        /// <summary>Ainda não estava na lixeira: vira UPDATE com DeletedAt/By.</summary>
        SoftDelete,

        /// <summary>Já estava na lixeira: DELETE real.</summary>
        Purge,

        /// <summary>Já estava na lixeira e só foi marcado pelo cascade de um pai que agora vai para a lixeira: fica como está.</summary>
        Keep,
    }

    /// <summary>
    /// Para cada entrada <c>Deleted</c>:
    /// <list type="bullet">
    /// <item><see cref="ISoftDeletable"/> ainda ativo → <c>Modified</c> com <c>DeletedAt = now</c> (o mesmo
    /// instante para todas as entradas, inclusive filhos marcados pelo cascade do EF: é assim que voltam
    /// juntos na restauração) e <c>DeletedById/Name</c> do ator.</item>
    /// <item><see cref="ISoftDeletable"/> já na lixeira → continua <c>Deleted</c> (expurgo), salvo quando o
    /// pai vai para a lixeira agora (fica como está, com o <c>DeletedAt</c> antigo).</item>
    /// <item>Filho cujo pai é expurgado → expurgado também.</item>
    /// <item>Tipos owned de quem vai para a lixeira → voltam a <c>Unchanged</c>.</item>
    /// </list>
    /// </summary>
    public static void Apply(DbContext context, AuditActor actor, DateTimeOffset now)
    {
        if (context.ChangeTracker.AutoDetectChangesEnabled)
        {
            context.ChangeTracker.DetectChanges();
        }

        var deleted = context.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).ToList();
        if (!deleted.Any(e => e.Entity is ISoftDeletable))
        {
            return;
        }

        var decisions = Decide(deleted);
        foreach (var entry in deleted)
        {
            if (!decisions.TryGetValue(entry.Entity, out var decision))
            {
                continue;
            }

            switch (decision)
            {
                case Decision.SoftDelete when entry.Entity is ISoftDeletable:
                    MarkDeleted(entry, actor, now);
                    break;
                case Decision.SoftDelete or Decision.Keep:
                    // Owned de quem foi para a lixeira, ou item já excluído que o cascade marcou.
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }
    }

    /// <summary>Decisão por entidade (só para <see cref="ISoftDeletable"/> e owned delas).</summary>
    internal static Dictionary<object, Decision> Decide(IReadOnlyList<EntityEntry> deleted)
    {
        var byKey = new Dictionary<(IEntityType, string), EntityEntry>();
        foreach (var entry in deleted)
        {
            if (entry.Metadata.FindPrimaryKey() is { } pk)
            {
                byKey.TryAdd((entry.Metadata.GetRootType(), KeyString(pk.Properties.Select(p => entry.Property(p.Name).CurrentValue))), entry);
            }
        }

        var decisions = new Dictionary<object, Decision>(ReferenceEqualityComparer.Instance);
        var visiting = new HashSet<object>(ReferenceEqualityComparer.Instance);

        Decision? DecideEntry(EntityEntry entry)
        {
            if (decisions.TryGetValue(entry.Entity, out var known))
            {
                return known;
            }

            if (!visiting.Add(entry.Entity))
            {
                return null; // ciclo: ignora o pai
            }

            var principal = FindPrincipal(entry, byKey);
            var principalDecision = principal is null ? null : DecideEntry(principal);
            visiting.Remove(entry.Entity);

            Decision? decision = entry.Entity switch
            {
                ISoftDeletable when principalDecision == Decision.Purge => Decision.Purge,
                ISoftDeletable soft when principalDecision is not null => soft.DeletedAt is null ? Decision.SoftDelete : Decision.Keep,
                ISoftDeletable soft => soft.DeletedAt is null ? Decision.SoftDelete : Decision.Purge,
                _ when entry.Metadata.IsOwned() && principalDecision is Decision.SoftDelete or Decision.Keep => Decision.Keep,
                _ => null,
            };

            if (decision is not null)
            {
                decisions[entry.Entity] = decision.Value;
            }

            return decision;
        }

        foreach (var entry in deleted)
        {
            DecideEntry(entry);
        }

        return decisions;
    }

    private static EntityEntry? FindPrincipal(EntityEntry entry, Dictionary<(IEntityType, string), EntityEntry> byKey)
    {
        foreach (var fk in entry.Metadata.GetForeignKeys())
        {
            if (!fk.PrincipalKey.IsPrimaryKey())
            {
                continue;
            }

            var values = fk.Properties.Select(p => entry.Property(p.Name).CurrentValue).ToList();
            if (values.Any(v => v is null))
            {
                continue;
            }

            if (byKey.TryGetValue((fk.PrincipalEntityType.GetRootType(), KeyString(values)), out var principal) && principal != entry)
            {
                return principal;
            }
        }

        return null;
    }

    private static string KeyString(IEnumerable<object?> values) => string.Join('\u001f', values.Select(v => v?.ToString()));

    private static void MarkDeleted(EntityEntry entry, AuditActor actor, DateTimeOffset now)
    {
        entry.State = EntityState.Modified;

        // Modified marca todas as colunas; mantém no UPDATE só o que realmente mudou.
        foreach (var property in entry.Properties)
        {
            if (!property.Metadata.IsPrimaryKey())
            {
                property.IsModified = !property.Metadata.GetValueComparer().Equals(property.OriginalValue, property.CurrentValue);
            }
        }

        var name = actor.Name.Length > SoftDelete.NameMaxLength ? actor.Name[..SoftDelete.NameMaxLength] : actor.Name;
        entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
        entry.Property(nameof(ISoftDeletable.DeletedById)).CurrentValue = actor.UserId;
        entry.Property(nameof(ISoftDeletable.DeletedByName)).CurrentValue = name;
    }
}
