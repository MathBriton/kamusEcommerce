using Kamus.Audit.Application;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Shared.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kamus.IntegrationTests.Audit;

/// <summary>A tabela <c>audit.entries</c> é somente inclusão: quem garante é o banco, não a aplicação.</summary>
public sealed class AuditTrailTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly AuditActor Robot = AuditActor.System("Teste");

    private async Task<int> ExecuteAsync(string sql, params object[] args)
    {
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand(sql);
        foreach (var arg in args)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = arg });
        }

        return await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<long> CountAsync(string module)
    {
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand("SELECT count(*) FROM audit.entries WHERE module = $1");
        command.Parameters.Add(new NpgsqlParameter { Value = module });
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task Update_direto_e_recusado_pelo_banco()
    {
        var module = AuditSeed.NewModule();
        await AuditSeed.WriteAsync(factory, AuditSeed.Record(module, DateTimeOffset.UtcNow, Robot));

        var update = () => ExecuteAsync("UPDATE audit.entries SET actor_name = 'Outro' WHERE module = $1", module);

        (await update.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("somente inclusão");
    }

    [Fact]
    public async Task Delete_de_registro_recente_e_recusado_pelo_banco()
    {
        var module = AuditSeed.NewModule();
        await AuditSeed.WriteAsync(factory, AuditSeed.Record(module, DateTimeOffset.UtcNow.AddDays(-300), Robot));

        var delete = () => ExecuteAsync("DELETE FROM audit.entries WHERE module = $1", module);

        (await delete.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("365 dias");
        (await CountAsync(module)).Should().Be(1);
    }

    [Fact]
    public async Task Truncate_e_recusado_pelo_banco()
    {
        // Numa transação sempre desfeita: se a proteção regredir, este teste falha sem apagar a trilha
        // que os outros testes (mesmo banco, em paralelo) estão lendo.
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await using var command = new NpgsqlCommand("SET LOCAL lock_timeout = '2s'; TRUNCATE audit.entries", connection, transaction);

        var truncate = () => command.ExecuteNonQueryAsync(Ct);

        try
        {
            (await truncate.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("TRUNCATE");
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Retencao_apaga_so_o_que_passou_do_prazo()
    {
        var module = AuditSeed.NewModule();
        var now = DateTimeOffset.UtcNow;
        await AuditSeed.WriteAsync(factory,
            AuditSeed.Record(module, now.AddDays(-800), Robot),
            AuditSeed.Record(module, now.AddDays(-400), Robot),
            AuditSeed.Record(module, now.AddDays(-1), Robot));

        var deleted = await factory.Services.GetRequiredService<AuditRetentionWorker>().RunOnceAsync(Ct);

        deleted.Should().BeGreaterThanOrEqualTo(1);
        (await CountAsync(module)).Should().Be(2, "a retenção padrão é de 730 dias");
    }

    [Fact]
    public async Task Delete_de_registro_com_mais_de_365_dias_passa_pelo_trigger()
    {
        var module = AuditSeed.NewModule();
        await AuditSeed.WriteAsync(factory, AuditSeed.Record(module, DateTimeOffset.UtcNow.AddDays(-400), Robot));

        (await ExecuteAsync("DELETE FROM audit.entries WHERE module = $1", module)).Should().Be(1);
    }
}
