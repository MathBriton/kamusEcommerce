using System.Globalization;
using System.Text.Json;
using Kamus.Audit.Api;
using Kamus.Audit.Persistence;
using Kamus.Shared.Auditing;
using Kamus.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Audit.Application;

/// <summary>Filtros da tela Atividade (e do CSV). Datas em UTC; <c>To</c> é inclusivo.</summary>
internal sealed record AuditFilter(string? Actor, string? Module, string? Action, DateOnly? From, DateOnly? To);

internal sealed class AuditQueries(AuditDbContext db)
{
    public const string SystemActorPrefix = "system:";
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int CsvLimit = 10_000;
    public const int DefaultSubjectLimit = 200;
    public const int MaxSubjectLimit = 500;

    /// <summary>Teto de página: além disso, <c>(página - 1) * tamanho</c> estoura o inteiro.</summary>
    public const int MaxPage = 100_000;

    /// <summary>Período aceito nos filtros; datas fora dele (ex.: 9999-12-31) não fazem sentido e estourariam o cálculo do fim do dia.</summary>
    private static readonly DateOnly MinDate = new(2000, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    public async Task<Result<AuditEntriesPage>> ListAsync(AuditFilter filter, int? page, int? pageSize, CancellationToken ct)
    {
        var query = Filter(filter);
        if (query.IsFailure)
        {
            return query.Error!;
        }

        var (number, size) = (Math.Clamp(page ?? 1, 1, MaxPage), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
        var total = await query.Value.CountAsync(ct);
        var rows = await Newest(query.Value).Skip((number - 1) * size).Take(size).ToListAsync(ct);

        return new AuditEntriesPage([.. rows.Select(ToDto)], total, number, size);
    }

    /// <summary>Até <see cref="CsvLimit"/> registros, mais recentes primeiro.</summary>
    public async Task<Result<IReadOnlyList<AuditEntryDto>>> ExportAsync(AuditFilter filter, CancellationToken ct)
    {
        var query = Filter(filter);
        if (query.IsFailure)
        {
            return query.Error!;
        }

        var rows = await Newest(query.Value).Take(CsvLimit).ToListAsync(ct);
        return Result.Success<IReadOnlyList<AuditEntryDto>>([.. rows.Select(ToDto)]);
    }

    /// <summary>Histórico de um agregado (produto, pedido), mais recente primeiro.</summary>
    public async Task<IReadOnlyList<AuditEntryDto>> SubjectAsync(string subjectType, Guid subjectId, int? limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? DefaultSubjectLimit, 1, MaxSubjectLimit);
        var rows = await Newest(db.Entries.AsNoTracking().Where(e => e.SubjectType == subjectType && e.SubjectId == subjectId))
            .Take(take)
            .ToListAsync(ct);
        return [.. rows.Select(ToDto)];
    }

    /// <summary>
    /// Quem aparece na auditoria: usuários (pelo id, com o nome e o e-mail mais recentes) e atores
    /// automáticos (<c>system:&lt;nome&gt;</c>), ordenados por nome.
    /// </summary>
    public async Task<IReadOnlyList<AuditActorOptionDto>> ActorsAsync(CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<ActorRow>($"""
            SELECT u.actor_id::text AS key, u.actor_kind AS kind, u.actor_name AS name, u.actor_email AS email
            FROM (
                SELECT DISTINCT ON (actor_id) actor_id, actor_kind, actor_name, actor_email
                FROM audit.entries
                WHERE actor_id IS NOT NULL
                ORDER BY actor_id, occurred_at DESC, id DESC
            ) u
            UNION ALL
            SELECT DISTINCT 'system:' || actor_name AS key, 'System' AS kind, actor_name AS name, NULL AS email
            FROM audit.entries
            WHERE actor_id IS NULL
            """).ToListAsync(ct);

        var compare = CultureInfo.InvariantCulture.CompareInfo;
        return [.. rows
            .Order(Comparer<ActorRow>.Create((a, b) =>
            {
                var byName = compare.Compare(a.Name, b.Name, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
                return byName != 0 ? byName : string.CompareOrdinal(a.Key, b.Key);
            }))
            .Select(r => new AuditActorOptionDto(r.Key, r.Kind, r.Name, r.Email))];
    }

    internal Result<IQueryable<AuditEntry>> Filter(AuditFilter filter)
    {
        if (filter.From is { } first && (first < MinDate || first > MaxDate)
            || filter.To is { } last && (last < MinDate || last > MaxDate))
        {
            return Error.Validation("audit.invalid_period", "Informe datas entre 2000 e 2100.");
        }

        if (filter.From is { } start && filter.To is { } end && start > end)
        {
            return Error.Validation("audit.invalid_period", "A data inicial deve ser anterior ou igual à final.");
        }

        var query = db.Entries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            if (Guid.TryParse(filter.Actor, out var actorId))
            {
                query = query.Where(e => e.ActorId == actorId);
            }
            else if (filter.Actor.StartsWith(SystemActorPrefix, StringComparison.Ordinal) && filter.Actor.Length > SystemActorPrefix.Length)
            {
                var name = filter.Actor[SystemActorPrefix.Length..];
                query = query.Where(e => e.ActorId == null && e.ActorName == name);
            }
            else
            {
                return Error.Validation("audit.invalid_actor", "Usuário inválido: informe o id do usuário ou system:<nome>.");
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            query = query.Where(e => e.Module == filter.Module);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(e => e.Action == filter.Action);
        }

        if (filter.From is { } from)
        {
            var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(e => e.OccurredAt >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(e => e.OccurredAt < toUtc);
        }

        return Result.Success(query);
    }

    private static IQueryable<AuditEntry> Newest(IQueryable<AuditEntry> query) =>
        query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id);

    internal static AuditEntryDto ToDto(AuditEntry e) => new(
        e.Id,
        e.OccurredAt,
        e.Module,
        e.EntityType,
        e.EntityId,
        e.Action,
        e.SubjectType,
        e.SubjectId,
        e.SubjectLabel,
        e.Detail,
        JsonSerializer.Deserialize<List<AuditChange>>(e.Changes, PostgresAuditLog.Json) ?? [],
        new AuditActorDto(e.ActorKind, e.ActorId, e.ActorName, e.ActorEmail),
        e.CorrelationId,
        e.IpAddress);

    private sealed class ActorRow
    {
        public string Key { get; set; } = null!;

        public string Kind { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string? Email { get; set; }
    }
}
