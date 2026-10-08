using FluentValidation;
using Kamus.Catalog.Api;
using Kamus.Catalog.Application;
using Kamus.Catalog.Contracts;
using Kamus.Catalog.Persistence;
using Kamus.Catalog.Seed;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Catalog;

public sealed class CatalogModule : IModule
{
    public string Name => "catalog";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CatalogOptions>()
            .Bind(configuration.GetSection(CatalogOptions.SectionName))
            .Validate(o => o.TrashRetentionDays >= 1, "Catalog:TrashRetentionDays deve ser de pelo menos 1 dia.")
            .Validate(o => o.TrashPurgeInterval > TimeSpan.Zero, "Catalog:TrashPurgeInterval deve ser positivo.")
            .ValidateOnStart();

        // Soft delete (lixeira) e auditoria: ver CatalogAuditPolicy e ADRs 0014/0015.
        services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema, CatalogAuditPolicy.Configure);
        services.AddScoped<CatalogQueries>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<CatalogAdminService>();
        services.AddScoped<CatalogTrashService>();
        services.AddSingleton<TrashPurgeWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<TrashPurgeWorker>());
        services.AddScoped<IDataSeeder, CatalogSeeder>();
        services.AddValidatorsFromAssemblyContaining<CatalogModule>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        CatalogEndpoints.Map(endpoints);
        CatalogAdminEndpoints.Map(endpoints);
    }
}
