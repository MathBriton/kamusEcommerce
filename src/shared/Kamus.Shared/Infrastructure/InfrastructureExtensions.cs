using Kamus.Shared.Auditing;
using Kamus.Shared.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

namespace Kamus.Shared.Infrastructure;

public static class InfrastructureExtensions
{
    public const string PostgresConnectionName = "Postgres";
    public const string RedisConnectionName = "Redis";

    /// <summary>
    /// Registra a infraestrutura compartilhada pelo monólito: um único
    /// <see cref="NpgsqlDataSource"/> (cada módulo usa o próprio schema) e a conexão com o Redis.
    /// </summary>
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgres = configuration.GetConnectionString(PostgresConnectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{PostgresConnectionName} não configurada.");
        var redis = configuration.GetConnectionString(RedisConnectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{RedisConnectionName} não configurada.");

        postgres = ConnectionStrings.NormalizePostgres(postgres);
        redis = ConnectionStrings.NormalizeRedis(redis);

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(postgres).Build());
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddInProcessEvents();
        services.AddHttpContextAccessor();
        services.AddCurrentActor();

        return services;
    }
}
