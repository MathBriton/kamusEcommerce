using FluentValidation;
using Kamus.Inventory.Application;
using Kamus.Shared.Results;
using Kamus.Shared.Security;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Inventory.Api;

public sealed record AdjustStockRequest(int Quantity);

internal sealed class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator() => RuleFor(r => r.Quantity).InclusiveBetween(0, 100_000);
}

internal static class InventoryAdminEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapAdminGroup("inventory");

        group.MapPut("/skus/{skuId:guid}", async (Guid skuId, AdjustStockRequest request, InventoryService inventory, CancellationToken ct) =>
                (await inventory.AdjustAsync(skuId, request.Quantity, ct)).ToHttp())
            .Validate<AdjustStockRequest>();
    }
}
