using Kamus.IntegrationTests.Infrastructure;
using Kamus.Shared.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kamus.IntegrationTests.Audit;

/// <summary>Grava registros de auditoria direto pelo <see cref="IAuditLog"/> da API (como os interceptors fazem).</summary>
internal static class AuditSeed
{
    /// <summary>Módulo único por teste: isola os dados de testes que rodam em paralelo.</summary>
    public static string NewModule() => $"t{Guid.NewGuid():N}"[..20];

    public static AuditRecord Record(
        string module,
        DateTimeOffset occurredAt,
        AuditActor actor,
        string action = "updated",
        string entityType = "Product",
        Guid? subjectId = null,
        string? label = "Camisa de Linho",
        AuditChange[]? changes = null)
    {
        var id = subjectId ?? Guid.CreateVersion7();
        return new AuditRecord(
            Guid.CreateVersion7(occurredAt),
            occurredAt,
            module,
            entityType,
            id,
            action,
            "Product",
            id,
            label,
            "Areia · P",
            changes ?? [],
            actor,
            "0HN:TESTE",
            "10.0.0.7");
    }

    public static async Task WriteAsync(KamusApiFactory factory, params AuditRecord[] records)
    {
        var log = factory.Services.GetRequiredService<IAuditLog>();
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await log.WriteAsync(records, connection, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}
