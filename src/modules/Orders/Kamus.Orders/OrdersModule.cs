using FluentValidation;
using Kamus.Inventory.Contracts;
using Kamus.Orders.Api;
using Kamus.Orders.Application;
using Kamus.Orders.Contracts;
using Kamus.Orders.Persistence;
using Kamus.Payments.Contracts;
using Kamus.Shared.Events;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Orders;

public sealed class OrdersModule : IModule
{
    public string Name => "orders";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OrdersOptions>(configuration.GetSection(OrdersOptions.SectionName));
        services.AddModuleDbContext<OrdersDbContext>(OrdersDbContext.Schema, OrdersAudit.Configure);
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderQueries>();
        services.AddScoped<OrderAdminService>();
        services.AddScoped<IOrderReports>(sp => sp.GetRequiredService<OrderAdminService>());
        services.AddScoped<IOrderImageReferences, OrderImageReferences>();
        services.AddScoped<OrderEventHandlers>();
        services.AddScoped<IEventHandler<PaymentApproved>>(sp => sp.GetRequiredService<OrderEventHandlers>());
        services.AddScoped<IEventHandler<PaymentDeclined>>(sp => sp.GetRequiredService<OrderEventHandlers>());
        services.AddScoped<IEventHandler<ReservationExpired>>(sp => sp.GetRequiredService<OrderEventHandlers>());
        services.AddValidatorsFromAssemblyContaining<OrdersModule>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        OrdersEndpoints.Map(endpoints);
        OrdersAdminEndpoints.Map(endpoints);
    }
}
