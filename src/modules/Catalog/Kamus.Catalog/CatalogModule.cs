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
        services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
        services.AddScoped<CatalogQueries>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<CatalogAdminService>();
        services.AddScoped<IDataSeeder, CatalogSeeder>();
        services.AddValidatorsFromAssemblyContaining<CatalogModule>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        CatalogEndpoints.Map(endpoints);
        CatalogAdminEndpoints.Map(endpoints);
    }
}
