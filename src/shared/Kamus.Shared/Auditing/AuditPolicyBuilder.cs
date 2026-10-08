using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Kamus.Shared.Auditing;

/// <summary>Agregado exibido nas telas para um registro de auditoria (ex.: o produto de um SKU).</summary>
/// <param name="Type">"Product", "Order"...</param>
/// <param name="Label">Rótulo exibido: "Camisa de Linho Areia", "Pedido KM10003".</param>
/// <param name="Detail">Complemento opcional: "Areia · P".</param>
public sealed record AuditSubject(string Type, Guid Id, string? Label, string? Detail = null);

/// <summary>
/// Política de auditoria de um módulo, configurada no registro do <c>DbContext</c>:
/// <code>
/// services.AddModuleDbContext&lt;CatalogDbContext&gt;(CatalogDbContext.Schema, audit =&gt; audit
///     .Module("catalog")
///     .Entity&lt;Product&gt;(e =&gt; e
///         .Subject("Product", p =&gt; p.Id, p =&gt; p.Name)
///         .Track(p =&gt; p.Name, "Nome")
///         .Track(p =&gt; p.IsActive, "Situação", v =&gt; v is true ? "Publicado" : "Rascunho")
///         .Track(p =&gt; p.CategoryId, "Categoria", async (db, value, ct) =&gt; /* nome via db */)
///         .Action(entry =&gt; entry.HasChanged(p =&gt; p.IsActive)
///             ? (entry.Entity.IsActive ? "published" : "unpublished")
///             : null)));
/// </code>
/// </summary>
/// <remarks>
/// Nada disso vai para o modelo do EF (nem para as migrations): a política vive só nos interceptors.
/// Só entidades declaradas em <see cref="Entity{TEntity}"/> são auditadas, e só as propriedades com
/// <c>Track</c> viram "antes → depois" (allowlist). Nunca audite Identity nem Payments.
/// </remarks>
public sealed class AuditPolicyBuilder
{
    private readonly List<EntityAuditPolicy> _entities = [];
    private string? _module;

    /// <summary>Código do módulo gravado em cada registro: "catalog", "inventory", "orders" (até 30 caracteres).</summary>
    public AuditPolicyBuilder Module(string module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        if (module.Length > 30)
        {
            throw new ArgumentException("O código do módulo tem no máximo 30 caracteres.", nameof(module));
        }

        _module = module;
        return this;
    }

    /// <summary>
    /// Audita a entidade <typeparamref name="TEntity"/>. A ordem de declaração define a ordem dos
    /// registros gerados no mesmo <c>SaveChanges</c> (declare o agregado antes dos filhos).
    /// </summary>
    public AuditPolicyBuilder Entity<TEntity>(Action<AuditEntityBuilder<TEntity>> configure)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_entities.Any(e => e.ClrType == typeof(TEntity)))
        {
            throw new InvalidOperationException($"A entidade {typeof(TEntity).Name} já tem política de auditoria.");
        }

        var builder = new AuditEntityBuilder<TEntity>();
        configure(builder);
        _entities.Add(builder.Build(_entities.Count));
        return this;
    }

    internal AuditPolicy Build()
    {
        if (_module is null)
        {
            throw new InvalidOperationException("Política de auditoria sem módulo: chame Module(\"...\").");
        }

        return new AuditPolicy(_module, _entities);
    }
}

/// <summary>Configuração da auditoria de uma entidade (veja <see cref="AuditPolicyBuilder"/>).</summary>
public sealed class AuditEntityBuilder<TEntity>
    where TEntity : class
{
    private readonly List<TrackedProperty> _tracked = [];
    private Func<TEntity, AuditSubject>? _subject;
    private Func<IServiceProvider, TEntity, CancellationToken, ValueTask<AuditSubject>>? _subjectAsync;
    private Func<TEntity, string?>? _detail;
    private Func<EntityEntry<TEntity>, string?>? _action;

    internal AuditEntityBuilder()
    {
    }

    /// <summary>
    /// Agregado exibido (tipo, id e rótulo). Avaliado DEPOIS de gravar, então enxerga valores gerados
    /// no insert (ex.: número do pedido). Sem <c>Subject</c>, o agregado é a própria entidade, sem rótulo.
    /// </summary>
    public AuditEntityBuilder<TEntity> Subject(string type, Func<TEntity, Guid> id, Func<TEntity, string?>? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(id);
        _subject = entity => new AuditSubject(type, id(entity), label?.Invoke(entity));
        _subjectAsync = null;
        return this;
    }

    /// <summary>
    /// Agregado resolvido de forma assíncrona (ex.: nome do produto via contrato de outro módulo).
    /// Avaliado ANTES de gravar: o change tracker e o banco ainda mostram o estado anterior (útil em
    /// exclusões definitivas). Use consultas <c>AsNoTracking</c>. O <see cref="IServiceProvider"/> é o
    /// do escopo da requisição.
    /// </summary>
    public AuditEntityBuilder<TEntity> SubjectAsync(
        string type,
        Func<IServiceProvider, TEntity, CancellationToken, ValueTask<(Guid Id, string? Label, string? Detail)>> resolve)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(resolve);
        return SubjectAsync(async (sp, entity, ct) =>
        {
            var (id, label, detail) = await resolve(sp, entity, ct);
            return new AuditSubject(type, id, label, detail);
        });
    }

    /// <summary>
    /// Variante em que o próprio resolvedor decide o tipo do agregado (ex.: "Product" quando o SKU
    /// é encontrado, "Sku" como fallback).
    /// </summary>
    public AuditEntityBuilder<TEntity> SubjectAsync(Func<IServiceProvider, TEntity, CancellationToken, ValueTask<AuditSubject>> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        _subjectAsync = resolve;
        _subject = null;
        return this;
    }

    /// <summary>Complemento exibido ("Areia · P", "Cor Areia"). Avaliado depois de gravar; tem prioridade sobre o detalhe de <c>SubjectAsync</c>.</summary>
    public AuditEntityBuilder<TEntity> Detail(Func<TEntity, string?> detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        _detail = detail;
        return this;
    }

    /// <summary>Audita a propriedade com a formatação padrão (<see cref="AuditFormat.Value"/>).</summary>
    public AuditEntityBuilder<TEntity> Track<TValue>(Expression<Func<TEntity, TValue>> property, string label) =>
        Add(property, label, AuditFormat.Value, null, onlyOnCreate: false);

    /// <summary>Audita a propriedade com formatação própria (ex.: <c>v =&gt; v is true ? "Publicado" : "Rascunho"</c>).</summary>
    public AuditEntityBuilder<TEntity> Track<TValue>(Expression<Func<TEntity, TValue>> property, string label, Func<object?, string?> format) =>
        Add(property, label, format, null, onlyOnCreate: false);

    /// <summary>
    /// Audita a propriedade com formatação assíncrona, que recebe o <see cref="DbContext"/> que está
    /// gravando (ex.: trocar o id da categoria pelo nome). Roda antes de gravar; use <c>AsNoTracking</c>.
    /// </summary>
    public AuditEntityBuilder<TEntity> Track<TValue>(
        Expression<Func<TEntity, TValue>> property,
        string label,
        Func<DbContext, object?, CancellationToken, ValueTask<string?>> format) =>
        Add(property, label, null, format, onlyOnCreate: false);

    /// <summary>Como <c>Track</c>, mas só aparece no registro de criação (ex.: código do SKU).</summary>
    public AuditEntityBuilder<TEntity> TrackOnCreate<TValue>(Expression<Func<TEntity, TValue>> property, string label, Func<object?, string?>? format = null) =>
        Add(property, label, format ?? AuditFormat.Value, null, onlyOnCreate: true);

    /// <summary>
    /// Refina a ação de uma alteração. Consultado só quando a ação genérica seria
    /// <see cref="AuditActions.Updated"/> (não em criação, exclusão, restauração ou expurgo).
    /// Retorne o código refinado (ex.: "published", "paid", "stock_adjusted") ou <see langword="null"/>
    /// para manter "updated". Uma alteração com ação refinada é registrada mesmo que nenhum campo
    /// rastreado tenha mudado. Use <see cref="AuditEntryExtensions.HasChanged{TEntity, TProperty}"/>.
    /// </summary>
    public AuditEntityBuilder<TEntity> Action(Func<EntityEntry<TEntity>, string?> refine)
    {
        ArgumentNullException.ThrowIfNull(refine);
        _action = refine;
        return this;
    }

    internal EntityAuditPolicy Build(int order) =>
        new EntityAuditPolicy<TEntity>(order, [.. _tracked], _subject, _subjectAsync, _detail, _action);

    private AuditEntityBuilder<TEntity> Add<TValue>(
        Expression<Func<TEntity, TValue>> property,
        string label,
        Func<object?, string?>? format,
        Func<DbContext, object?, CancellationToken, ValueTask<string?>>? formatAsync,
        bool onlyOnCreate)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var body = property.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : property.Body;
        if (body is not MemberExpression { Member: PropertyInfo or FieldInfo } member || member.Expression != property.Parameters[0])
        {
            throw new ArgumentException($"Use um acesso direto a propriedade (ex.: x => x.Nome), não '{property}'.", nameof(property));
        }

        if (_tracked.Any(t => t.PropertyName == member.Member.Name))
        {
            throw new InvalidOperationException($"A propriedade {typeof(TEntity).Name}.{member.Member.Name} já é auditada.");
        }

        _tracked.Add(new TrackedProperty(member.Member.Name, label, format, formatAsync, onlyOnCreate));
        return this;
    }
}

/// <summary>Atalhos para as funções de refinamento de ação (<c>Action(...)</c>).</summary>
public static class AuditEntryExtensions
{
    /// <summary>O valor atual da propriedade é diferente do original (carregado do banco)?</summary>
    public static bool HasChanged<TEntity, TProperty>(this EntityEntry<TEntity> entry, Expression<Func<TEntity, TProperty>> property)
        where TEntity : class
    {
        var p = entry.Property(property);
        return !p.Metadata.GetValueComparer().Equals(p.OriginalValue, p.CurrentValue);
    }
}
