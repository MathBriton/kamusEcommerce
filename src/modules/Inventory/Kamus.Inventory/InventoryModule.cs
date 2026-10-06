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
        services.AddModuleDbContext<InventoryDbContext>(InventoryDbContext.Schema);
        services.AddScoped<IInventoryService, InventoryService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
