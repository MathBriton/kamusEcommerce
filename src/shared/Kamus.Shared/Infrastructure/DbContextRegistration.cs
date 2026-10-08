using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kamus.Shared.Infrastructure;

public static class DbContextRegistration
{
    /// <summary>
    /// Registra o <see cref="DbContext"/> de um módulo no schema do próprio módulo, com tabela de
    /// histórico de migrations separada e nomes em snake_case. Sem auditoria nem soft delete.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext =>
        services.AddModuleDbContext<TContext>(schema, policy: null);

    /// <summary>
    /// Como <see cref="AddModuleDbContext{TContext}(IServiceCollection, string)"/>, com auditoria e soft
    /// delete: registra a política montada em <paramref name="audit"/> e adiciona ao DbContext o
    /// <see cref="SoftDeleteInterceptor"/> e, depois dele, o <see cref="AuditingInterceptor"/>.
    /// </summary>
    /// <remarks>
    /// Exige SaveChangesAsync (o síncrono lança exceção quando há algo a auditar) e um
    /// <see cref="IAuditLog"/> registrado (módulo Audit). Veja <see cref="AuditPolicyBuilder"/>.
    /// </remarks>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema, Action<AuditPolicyBuilder> audit)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(audit);
        var builder = new AuditPolicyBuilder();
        audit(builder);
        return services.AddModuleDbContext<TContext>(schema, builder.Build());
    }

    private static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema, AuditPolicy? policy)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((sp, options) =>
        {
            options
                .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql
                    .MigrationsHistoryTable("__ef_migrations_history", schema)
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseSnakeCaseNamingConvention();

            if (policy is not null)
            {
                // O provider aqui é o do escopo (as opções do DbContext são scoped): os interceptors
                // enxergam o ICurrentActor da requisição. A ordem importa: soft delete antes da auditoria.
                options.AddInterceptors(
                    ActivatorUtilities.CreateInstance<SoftDeleteInterceptor>(sp),
                    ActivatorUtilities.CreateInstance<AuditingInterceptor>(sp, policy));
            }
        });

        services.AddScoped<IDatabaseMigrator, DbContextMigrator<TContext>>();

        return services;
    }
}
