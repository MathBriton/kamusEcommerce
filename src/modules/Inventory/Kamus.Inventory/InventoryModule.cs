using Kamus.Inventory.Application;
using Kamus.Inventory.Contracts;
using Kamus.Inventory.Persistence;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Inventory;

public sealed class InventoryModule : IModule
{
    public string Name => "inventory";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InventoryOptions>(configuration.GetSection(InventoryOptions.SectionName));
        services.AddModuleDbContext<InventoryDbContext>(InventoryDbContext.Schema);
        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryService>(sp => sp.GetRequiredService<InventoryService>());
        services.AddSingleton<ReservationExpiryWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<ReservationExpiryWorker>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
