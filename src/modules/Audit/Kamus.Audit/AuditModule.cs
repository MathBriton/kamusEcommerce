using Kamus.Audit.Api;
using Kamus.Audit.Application;
using Kamus.Audit.Persistence;
using Kamus.Shared.Auditing;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Audit;

/// <summary>
/// Trilha de auditoria: guarda os registros que os interceptors dos outros módulos produzem
/// (contrato <see cref="IAuditLog"/>, no Shared) e os expõe ao backoffice. Não tem <c>.Contracts</c>.
/// </summary>
public sealed class AuditModule : IModule
{
    public string Name => "audit";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuditOptions>()
            .Bind(configuration.GetSection(AuditOptions.SectionName))
            .Validate(o => o.RetentionDays >= AuditOptions.MinimumRetentionDays,
                $"Audit:RetentionDays deve ser de pelo menos {AuditOptions.MinimumRetentionDays} dias.")
            .Validate(o => o.CleanupInterval > TimeSpan.Zero, "Audit:CleanupInterval deve ser positivo.")
            .ValidateOnStart();

        services.AddModuleDbContext<AuditDbContext>(AuditDbContext.Schema);
        services.AddSingleton<IAuditLog, PostgresAuditLog>();
        services.AddScoped<AuditQueries>();
        services.AddSingleton<AuditRetentionWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<AuditRetentionWorker>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => AuditEndpoints.Map(endpoints);
}
