using System.Data.Common;
using System.Text.Json;
using Kamus.Shared.Auditing;
using Npgsql;
using NpgsqlTypes;

namespace Kamus.Audit.Persistence;

/// <summary>
/// Grava os registros em <c>audit.entries</c> na conexão e transação de quem alterou (um único
/// round-trip com <see cref="NpgsqlBatch"/>): se a alteração for desfeita, a auditoria também é.
/// </summary>
internal sealed class PostgresAuditLog : IAuditLog
{
    internal static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    private const string InsertSql = """
        INSERT INTO audit.entries (
            id, occurred_at, module, entity_type, entity_id, action, subject_type, subject_id,
            subject_label, detail, changes, actor_kind, actor_id, actor_name, actor_email,
            correlation_id, ip_address)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)
        """;

    public async Task WriteAsync(IReadOnlyList<AuditRecord> records, DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return;
        }

        if (connection is not NpgsqlConnection npgsql || transaction is not NpgsqlTransaction npgsqlTransaction)
        {
            throw new InvalidOperationException("A auditoria só grava em conexões do PostgreSQL (Npgsql).");
        }

        await using var batch = new NpgsqlBatch(npgsql, npgsqlTransaction);
        foreach (var record in records)
        {
            var command = new NpgsqlBatchCommand(InsertSql);
            var p = command.Parameters;
            p.Add(Value(NpgsqlDbType.Uuid, record.Id));
            p.Add(Value(NpgsqlDbType.TimestampTz, record.OccurredAt.ToUniversalTime()));
            p.Add(Text(record.Module, AuditColumns.Module));
            p.Add(Text(record.EntityType, AuditColumns.EntityType));
            p.Add(Value(NpgsqlDbType.Uuid, record.EntityId));
            p.Add(Text(record.Action, AuditColumns.Action));
            p.Add(Text(record.SubjectType, AuditColumns.SubjectType));
            p.Add(Value(NpgsqlDbType.Uuid, record.SubjectId));
            p.Add(Text(record.SubjectLabel, AuditColumns.SubjectLabel));
            p.Add(Text(record.Detail, AuditColumns.Detail));
            p.Add(Value(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(record.Changes, Json)));
            p.Add(Text(record.Actor.Kind.ToString(), AuditColumns.ActorKind));
            p.Add(Value(NpgsqlDbType.Uuid, record.Actor.UserId));
            p.Add(Text(record.Actor.Name, AuditColumns.ActorName));
            p.Add(Text(record.Actor.Email, AuditColumns.ActorEmail));
            p.Add(Text(record.CorrelationId, AuditColumns.CorrelationId));
            p.Add(Text(record.IpAddress, AuditColumns.IpAddress));
            batch.BatchCommands.Add(command);
        }

        await batch.ExecuteNonQueryAsync(ct);
    }

    private static NpgsqlParameter Value(NpgsqlDbType type, object? value) =>
        new() { NpgsqlDbType = type, Value = value ?? DBNull.Value };

    /// <summary>Texto cortado no tamanho da coluna: um rótulo longo não pode derrubar a alteração auditada.</summary>
    private static NpgsqlParameter Text(string? value, int maxLength) =>
        Value(NpgsqlDbType.Varchar, value is not null && value.Length > maxLength ? value[..maxLength] : value);
}
