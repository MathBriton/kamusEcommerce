using Kamus.Orders.Application;
using Kamus.Shared.Results;
using Kamus.Shared.Security;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Orders.Api;

internal static class OrdersAdminEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapAdminGroup("orders");

        group.MapGet("/", async (string? status, string? search, int? page, int? pageSize, OrderAdminService admin, CancellationToken ct) =>
        {
            var (p, size) = (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? 20, 1, 100));
            var (items, total) = await admin.ListAsync(status, search, p, size, ct);
            return TypedResults.Ok(new { items, total, page = p, pageSize = size });
        });

        group.MapGet("/{id:guid}", async (Guid id, OrderAdminService admin, CancellationToken ct) =>
            (await admin.GetAsync(id, ct)).ToHttp());

        group.MapPost("/{id:guid}/ship", async (Guid id, ShipOrderRequest request, OrderAdminService admin, CancellationToken ct) =>
                (await admin.ShipAsync(id, request.TrackingCode, ct)).ToHttp())
            .Validate<ShipOrderRequest>();

        group.MapPost("/{id:guid}/deliver", async (Guid id, OrderAdminService admin, CancellationToken ct) =>
            (await admin.DeliverAsync(id, ct)).ToHttp());

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest request, OrderAdminService admin, CancellationToken ct) =>
                (await admin.CancelAsync(id, request.Reason, ct)).ToHttp())
            .Validate<CancelOrderRequest>();
    }
}
