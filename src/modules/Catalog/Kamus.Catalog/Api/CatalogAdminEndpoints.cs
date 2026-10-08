using Kamus.Catalog.Application;
using Kamus.Shared.Results;
using Kamus.Shared.Security;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Catalog.Api;

internal static class CatalogAdminEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapAdminGroup("catalog");

        group.MapGet("/products", async (string? search, string? status, int? page, int? pageSize, CatalogAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.ListAsync(search, status, Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? 20, 1, 100), ct)));

        group.MapGet("/collections", async (CatalogAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.CollectionsAsync(ct)));

        group.MapGet("/products/{id:guid}", async (Guid id, CatalogAdminService admin, CancellationToken ct) =>
            (await admin.GetAsync(id, ct)).ToHttp());

        group.MapPost("/products", async (SaveProductRequest request, CatalogAdminService admin, CancellationToken ct) =>
            {
                var result = await admin.CreateAsync(request, ct);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/admin/catalog/products/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .Validate<SaveProductRequest>();

        group.MapPut("/products/{id:guid}", async (Guid id, SaveProductRequest request, CatalogAdminService admin, CancellationToken ct) =>
                (await admin.UpdateAsync(id, request, ct)).ToHttp())
            .Validate<SaveProductRequest>();

        group.MapPost("/products/{id:guid}/publish", async (Guid id, CatalogAdminService admin, CancellationToken ct) =>
            (await admin.SetActiveAsync(id, active: true, ct)).ToHttp());

        group.MapPost("/products/{id:guid}/unpublish", async (Guid id, CatalogAdminService admin, CancellationToken ct) =>
            (await admin.SetActiveAsync(id, active: false, ct)).ToHttp());

        group.MapPost("/products/{id:guid}/skus", async (Guid id, AddSkusRequest request, CatalogAdminService admin, CancellationToken ct) =>
                (await admin.AddSkusAsync(id, request, ct)).ToHttp())
            .Validate<AddSkusRequest>();

        group.MapPut("/skus/{skuId:guid}/prices", async (Guid skuId, UpdateSkuPricesRequest request, CatalogAdminService admin, CancellationToken ct) =>
                (await admin.UpdateSkuPricesAsync(skuId, request, ct)).ToHttp())
            .Validate<UpdateSkuPricesRequest>();

        group.MapPost("/products/{id:guid}/images", async (Guid id, IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] string color, CatalogAdminService admin, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return (await admin.AddImageAsync(id, color, stream, file.Length, ct)).ToHttp();
            })
            // Sem token antiforgery: a rota exige o papel Admin e o cookie é SameSite=Lax, então
            // um formulário de outro site não envia a sessão (ver ADR 0010).
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(CatalogAdminService.MaxImageBytes + 64 * 1024));

        // Exclusões vão para a lixeira (soft delete); só o expurgo apaga de vez.
        group.MapDelete("/products/{id:guid}/images/{imageId:guid}", async (Guid id, Guid imageId, CatalogAdminService admin, CancellationToken ct) =>
            (await admin.RemoveImageAsync(id, imageId, ct)).ToHttp());

        group.MapDelete("/products/{id:guid}", async (Guid id, CatalogAdminService admin, CancellationToken ct) =>
            (await admin.DeleteProductAsync(id, ct)).ToHttp());

        group.MapDelete("/skus/{skuId:guid}", async (Guid skuId, CatalogAdminService admin, CancellationToken ct) =>
            (await admin.DeleteSkuAsync(skuId, ct)).ToHttp());

        // Lixeira: type = product | sku | image.
        group.MapGet("/trash", async (string? type, int? page, int? pageSize, CatalogTrashService trash, CancellationToken ct) =>
            (await trash.ListAsync(type, Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? 50, 1, 100), ct)).ToHttp());

        group.MapPost("/trash/{type}/{id:guid}/restore", async (string type, Guid id, CatalogTrashService trash, CancellationToken ct) =>
            (await trash.RestoreAsync(type, id, ct)).ToHttp());

        group.MapDelete("/trash/{type}/{id:guid}", async (string type, Guid id, CatalogTrashService trash, CancellationToken ct) =>
            (await trash.PurgeAsync(type, id, ct)).ToHttp());
    }
}
