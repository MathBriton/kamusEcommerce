using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamus.Shared.Auditing;

/// <summary>
/// Entidade com exclusão lógica (lixeira). Exponha só getters; os setters ficam privados na entidade
/// e quem escreve é o <see cref="SoftDeleteInterceptor"/> (via <c>entry.Property(...).CurrentValue</c>).
/// </summary>
/// <remarks>
/// Fluxo: <c>db.Remove(entidade)</c> numa entidade ativa vira UPDATE (vai para a lixeira, com quem e
/// quando); <c>db.Remove</c> numa entidade que já está na lixeira vira DELETE real (expurgo).
/// Para restaurar, zere os três campos com <see cref="SoftDelete.Restore(EntityEntry)"/> ou com um método
/// <c>Restore()</c> da própria entidade.
/// </remarks>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedById { get; }

    string? DeletedByName { get; }
}

/// <summary>Atalhos para entidades <see cref="ISoftDeletable"/>.</summary>
public static class SoftDelete
{
    /// <summary>Tamanho máximo da coluna <c>deleted_by_name</c> (igual ao nome do ator na auditoria).</summary>
    public const int NameMaxLength = 150;

    public static bool IsDeleted(this ISoftDeletable entity) => entity.DeletedAt is not null;

    /// <summary>Tira a entidade da lixeira: zera <c>DeletedAt</c>, <c>DeletedById</c> e <c>DeletedByName</c>.</summary>
    public static void Restore(this EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable)
        {
            throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} não implementa {nameof(ISoftDeletable)}.");
        }

        entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = null;
        entry.Property(nameof(ISoftDeletable.DeletedById)).CurrentValue = null;
        entry.Property(nameof(ISoftDeletable.DeletedByName)).CurrentValue = null;
    }

    /// <summary>Atalho para <see cref="Restore(EntityEntry)"/> a partir do <see cref="DbContext"/>.</summary>
    public static void Restore(this DbContext db, ISoftDeletable entity) => db.Entry(entity).Restore();

    /// <summary>
    /// Configuração opcional no <c>OnModelCreating</c>: filtro global que esconde o que está na lixeira
    /// (<c>DeletedAt == null</c>) e tamanho da coluna do nome. Para ler a lixeira, use
    /// <c>IgnoreQueryFilters()</c>. Índices únicos continuam por conta do módulo (use
    /// <c>HasFilter("deleted_at IS NULL")</c> para que o valor possa ser reaproveitado).
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasSoftDelete<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ISoftDeletable
    {
        builder.Property<string?>(nameof(ISoftDeletable.DeletedByName)).HasMaxLength(NameMaxLength);
        builder.HasQueryFilter(e => e.DeletedAt == null);
        return builder;
    }
}
