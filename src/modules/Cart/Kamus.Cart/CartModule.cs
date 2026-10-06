using FluentValidation;
using Kamus.Cart.Api;
using Kamus.Cart.Application;
using Kamus.Cart.Contracts;
using Kamus.Identity.Contracts;
using Kamus.Shared.Events;
using Kamus.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Cart;

public sealed class CartModule : IModule
{
    public string Name => "cart";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CartOptions>(configuration.GetSection(CartOptions.SectionName));
        services.AddSingleton<CartStore>();
        services.AddScoped<CartOwnerResolver>();
        services.AddScoped<CartApplication>();
        services.AddScoped<ICartService>(sp => sp.GetRequiredService<CartApplication>());
        services.AddScoped<IEventHandler<CustomerSignedIn>>(sp => sp.GetRequiredService<CartApplication>());
        services.AddValidatorsFromAssemblyContaining<CartModule>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => CartEndpoints.Map(endpoints);
}
