using Kamus.Orders.Application;
using Kamus.Shared.Results;
using Kamus.Shared.Security;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Kamus.Orders.Api;

internal static class OrdersEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var checkout = endpoints.MapGroup("/api/checkout").WithTags("Checkout").RequireAuthorization();

        checkout.MapGet("/", async (HttpContext http, CheckoutService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(http.User.GetRequiredCustomerId(), ct)));

        checkout.MapGet("/shipping", (string state, decimal subtotal) =>
            CheckoutService.QuoteShipping(state, subtotal).ToHttp());

        var orders = endpoints.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();

        orders.MapPost("/", async (PlaceOrderRequest request, HttpContext http, CheckoutService service, CancellationToken ct) =>
            {
                var result = await service.PlaceOrderAsync(http.User.GetRequiredCustomerId(), request, ct);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/orders/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .Validate<PlaceOrderRequest>();

        orders.MapGet("/", async (HttpContext http, OrderQueries queries, CancellationToken ct) =>
            TypedResults.Ok(await queries.ListAsync(http.User.GetRequiredCustomerId(), ct)));

        orders.MapGet("/{id:guid}", async (Guid id, HttpContext http, OrderQueries queries, CancellationToken ct) =>
            (await queries.GetAsync(http.User.GetRequiredCustomerId(), id, ct)).ToHttp());

        orders.MapPost("/{id:guid}/cancel", async (Guid id, HttpContext http, OrderQueries queries, CancellationToken ct) =>
            (await queries.CancelAsync(http.User.GetRequiredCustomerId(), id, ct)).ToHttp());

        orders.MapPost("/{id:guid}/advance-fulfillment", async (Guid id, HttpContext http, OrderQueries queries, IOptions<OrdersOptions> options, CancellationToken ct) =>
            options.Value.EnableFulfillmentSimulation
                ? (await queries.AdvanceFulfillmentAsync(http.User.GetRequiredCustomerId(), id, ct)).ToHttp()
                : TypedResults.NotFound());
    }
}
