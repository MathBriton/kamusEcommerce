using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Shared.Auditing;

/// <summary>
/// Captura as alterações das entidades com política e grava os registros pelo <see cref="IAuditLog"/>
/// na mesma transação do SaveChanges (atômico: ou grava a alteração e a auditoria, ou nada).
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><c>SavingChangesAsync</c>: calcula ação e diff (antes do banco aceitar as mudanças) e, se não
/// houver transação corrente, abre uma.</item>
/// <item><c>SavedChangesAsync</c>: resolve rótulos que dependem de valores gerados no insert, grava os
/// registros e faz commit da transação que abriu.</item>
/// <item>Falha/cancelamento: rollback da transação que abriu. Se o SaveChanges for refeito (ex.:
/// <c>ConcurrencyRetry</c>), a nova tentativa abre outra transação.</item>
/// </list>
/// Não audita <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> nem SQL direto. SaveChanges síncrono com algo a
/// auditar lança <see cref="InvalidOperationException"/>.
/// </remarks>
internal sealed class AuditingInterceptor(
    AuditPolicy policy,
    ICurrentActor currentActor,
    TimeProvider clock,
    IServiceProvider services) : SaveChangesInterceptor
{
    private readonly Dictionary<DbContext, PendingSave> _saves = new(ReferenceEqualityComparer.Instance);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context && !currentActor.IsAuditingSuppressed && AuditableEntries(context).Any())
        {
            throw new InvalidOperationException(
                $"{context.GetType().Name} tem auditoria: use SaveChangesAsync (o SaveChanges síncrono não grava a auditoria).");
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } context)
        {
            return result;
        }

        // Sobra de uma tentativa anterior que falhou: descarta antes de começar de novo.
        await DiscardAsync(context);

        if (currentActor.IsAuditingSuppressed)
        {
            return result;
        }

        var audits = new List<PendingAudit>();
        var sequence = 0;
        foreach (var (entry, entityPolicy) in AuditableEntries(context).ToList())
        {
            var action = AuditAnalyzer.ResolveAction(entry, entityPolicy);
            if (action is null)
            {
                continue;
            }

            var changes = await AuditAnalyzer.ChangesAsync(entry, entityPolicy, cancellationToken);
            if (!AuditAnalyzer.ShouldRecord(action, changes))
            {
                continue;
            }

            var (id, isTemporary) = AuditAnalyzer.ReadKey(entry);
            audits.Add(new PendingAudit(entry, entityPolicy, action, changes, sequence++)
            {
                EntityId = isTemporary ? null : id,
                SubjectBeforeSave = entityPolicy.HasAsyncSubject
                    ? await entityPolicy.ResolveSubjectBeforeSaveAsync(services, entry.Entity, cancellationToken)
                    : null,
            });
        }

        if (audits.Count == 0)
        {
            return result;
        }

        var owned = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        _saves[context] = new PendingSave(audits, owned);
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } context || !_saves.Remove(context, out var save))
        {
            return result;
        }

        try
        {
            var transaction = context.Database.CurrentTransaction
                ?? throw new InvalidOperationException("A transação da auditoria foi encerrada antes do fim do SaveChanges.");
            var log = services.GetService<IAuditLog>()
                ?? throw new InvalidOperationException("Nenhum IAuditLog registrado: o módulo Audit precisa estar no ModuleRegistry.");

            await log.WriteAsync(BuildRecords(save.Audits), context.Database.GetDbConnection(), transaction.GetDbTransaction(), cancellationToken);

            if (save.OwnedTransaction is not null)
            {
                await save.OwnedTransaction.CommitAsync(cancellationToken);
                await save.OwnedTransaction.DisposeAsync();
            }
        }
        catch
        {
            await RollbackAsync(save);
            throw;
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) =>
        DiscardAsync(eventData.Context).AsTask();

    public override void SaveChangesCanceled(DbContextEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default) =>
        DiscardAsync(eventData.Context).AsTask();

    private IEnumerable<(EntityEntry Entry, EntityAuditPolicy Policy)> AuditableEntries(DbContext context)
    {
        if (context.ChangeTracker.AutoDetectChangesEnabled)
        {
            context.ChangeTracker.DetectChanges();
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && policy.Find(entry.Metadata.ClrType) is { } entityPolicy)
            {
                yield return (entry, entityPolicy);
            }
        }
    }

    private List<AuditRecord> BuildRecords(List<PendingAudit> audits)
    {
        var actor = currentActor.Actor;
        var (correlationId, ipAddress) = (currentActor.CorrelationId, currentActor.IpAddress);
        var occurredAt = clock.GetUtcNow();

        var ordered = audits.OrderBy(a => a.Policy.Order).ThenBy(a => a.Sequence).ToList();
        var records = new List<AuditRecord>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var audit = ordered[i];
            var entityId = audit.EntityId ?? AuditAnalyzer.ReadKey(audit.Entry).Id;
            var subject = audit.SubjectBeforeSave
                ?? audit.Policy.ResolveSubjectAfterSave(audit.Entity)
                ?? new AuditSubject(audit.Policy.EntityTypeName, entityId, null);

            records.Add(new AuditRecord(
                AuditIds.Sequential(occurredAt, i),
                occurredAt,
                policy.Module,
                audit.Policy.EntityTypeName,
                entityId,
                audit.Action,
                subject.Type,
                subject.Id,
                subject.Label,
                audit.Policy.ResolveDetail(audit.Entity) ?? subject.Detail,
                audit.Changes,
                actor,
                correlationId,
                ipAddress));
        }

        return records;
    }

    private async ValueTask DiscardAsync(DbContext? context)
    {
        if (context is not null && _saves.Remove(context, out var save))
        {
            await RollbackAsync(save);
        }
    }

    private void Discard(DbContext? context)
    {
        if (context is null || !_saves.Remove(context, out var save) || save.OwnedTransaction is not { } transaction)
        {
            return;
        }

        try
        {
            transaction.Rollback();
        }
        catch (Exception)
        {
            // Melhor esforço: a falha original é a que importa; descartar a transação basta.
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private static async ValueTask RollbackAsync(PendingSave save)
    {
        if (save.OwnedTransaction is not { } transaction)
        {
            return;
        }

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Melhor esforço: a falha original é a que importa; descartar a transação basta.
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private sealed record PendingSave(List<PendingAudit> Audits, IDbContextTransaction? OwnedTransaction);
}
