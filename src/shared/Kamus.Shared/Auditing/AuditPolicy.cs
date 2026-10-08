using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Kamus.Shared.Auditing;

/// <summary>Política montada por <see cref="AuditPolicyBuilder"/>: módulo + entidades auditadas.</summary>
internal sealed class AuditPolicy
{
    private readonly IReadOnlyList<EntityAuditPolicy> _entities;
    private readonly ConcurrentDictionary<Type, EntityAuditPolicy?> _byType = new();

    public AuditPolicy(string module, IReadOnlyList<EntityAuditPolicy> entities)
    {
        Module = module;
        _entities = entities;
    }

    public string Module { get; }

    /// <summary>Política da entidade (considera herança), ou <see langword="null"/> se ela não é auditada.</summary>
    public EntityAuditPolicy? Find(Type clrType) => _byType.GetOrAdd(clrType, type =>
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var match = _entities.FirstOrDefault(e => e.ClrType == current);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    });
}

internal sealed record TrackedProperty(
    string PropertyName,
    string Label,
    Func<object?, string?>? Format,
    Func<DbContext, object?, CancellationToken, ValueTask<string?>>? FormatAsync,
    bool OnlyOnCreate)
{
    public ValueTask<string?> FormatValueAsync(DbContext db, object? value, CancellationToken ct) =>
        FormatAsync is not null ? FormatAsync(db, value, ct) : ValueTask.FromResult((Format ?? AuditFormat.Value)(value));
}

internal abstract class EntityAuditPolicy(int order, Type clrType, IReadOnlyList<TrackedProperty> tracked)
{
    /// <summary>Ordem de declaração: define a ordem dos registros de um mesmo SaveChanges.</summary>
    public int Order { get; } = order;

    public Type ClrType { get; } = clrType;

    /// <summary>Nome gravado em <c>entity_type</c>: o nome da classe ("Product", "Sku"...).</summary>
    public string EntityTypeName { get; } = clrType.Name;

    public IReadOnlyList<TrackedProperty> Tracked { get; } = tracked;

    public abstract bool HasAsyncSubject { get; }

    /// <summary>Refinamento de "updated" configurado em <c>Action(...)</c>.</summary>
    public abstract string? RefineAction(EntityEntry entry);

    /// <summary>Agregado de <c>SubjectAsync</c>: roda antes de gravar.</summary>
    public abstract ValueTask<AuditSubject?> ResolveSubjectBeforeSaveAsync(IServiceProvider services, object entity, CancellationToken ct);

    /// <summary>Agregado de <c>Subject</c>: roda depois de gravar (valores gerados no insert já disponíveis).</summary>
    public abstract AuditSubject? ResolveSubjectAfterSave(object entity);

    public abstract string? ResolveDetail(object entity);
}

internal sealed class EntityAuditPolicy<TEntity>(
    int order,
    IReadOnlyList<TrackedProperty> tracked,
    Func<TEntity, AuditSubject>? subject,
    Func<IServiceProvider, TEntity, CancellationToken, ValueTask<AuditSubject>>? subjectAsync,
    Func<TEntity, string?>? detail,
    Func<EntityEntry<TEntity>, string?>? action)
    : EntityAuditPolicy(order, typeof(TEntity), tracked)
    where TEntity : class
{
    public override bool HasAsyncSubject => subjectAsync is not null;

    public override string? RefineAction(EntityEntry entry) =>
        action?.Invoke(entry.Context.Entry((TEntity)entry.Entity));

    public override async ValueTask<AuditSubject?> ResolveSubjectBeforeSaveAsync(IServiceProvider services, object entity, CancellationToken ct) =>
        subjectAsync is null ? null : await subjectAsync(services, (TEntity)entity, ct);

    public override AuditSubject? ResolveSubjectAfterSave(object entity) => subject?.Invoke((TEntity)entity);

    public override string? ResolveDetail(object entity) => detail?.Invoke((TEntity)entity);
}
