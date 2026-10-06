using FluentValidation;
using Kamus.Cart.Application;
using Kamus.Shared.Results;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Cart.Api;

public sealed record AddCartItemRequest(Guid SkuId, int Quantity = 1);

public sealed record UpdateCartItemRequest(int Quantity);

internal sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(r => r.SkuId).NotEmpty();
        RuleFor(r => r.Quantity).InclusiveBetween(1, 10);
    }
}

internal sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator() => RuleFor(r => r.Quantity).InclusiveBetween(1, 10);
}

internal static class CartEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/cart").WithTags("Cart");

        group.MapGet("/", async (CartApplication cart, CancellationToken ct) => TypedResults.Ok(await cart.GetCurrentAsync(ct)));

        group.MapGet("/count", async (CartApplication cart) => TypedResults.Ok(new { count = await cart.CountAsync() }));

        group.MapPost("/items", async (AddCartItemRequest request, CartApplication cart, CancellationToken ct) =>
                (await cart.AddAsync(request.SkuId, request.Quantity, ct)).ToHttp())
            .Validate<AddCartItemRequest>();

        group.MapPut("/items/{skuId:guid}", async (Guid skuId, UpdateCartItemRequest request, CartApplication cart, CancellationToken ct) =>
                (await cart.SetQuantityAsync(skuId, request.Quantity, ct)).ToHttp())
            .Validate<UpdateCartItemRequest>();

        group.MapDelete("/items/{skuId:guid}", async (Guid skuId, CartApplication cart, CancellationToken ct) =>
            TypedResults.Ok(await cart.RemoveAsync(skuId, ct)));
    }
}
