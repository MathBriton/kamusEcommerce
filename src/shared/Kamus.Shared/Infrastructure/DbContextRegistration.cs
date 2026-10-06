using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kamus.Shared.Infrastructure;

public static class DbContextRegistration
{
    /// <summary>
    /// Registra o <see cref="DbContext"/> de um módulo no schema do próprio módulo, com tabela de
    /// histórico de migrations separada e nomes em snake_case.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql
                .MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IDatabaseMigrator, DbContextMigrator<TContext>>();

        return services;
    }
}
