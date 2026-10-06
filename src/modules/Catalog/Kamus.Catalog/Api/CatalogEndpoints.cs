using Kamus.Catalog.Application;
using Kamus.Shared.Results;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Catalog.Api;

internal static class CatalogEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/catalog").WithTags("Catalog");

        group.MapGet("/categories", async (CatalogQueries queries, CancellationToken ct) =>
            TypedResults.Ok(await queries.GetCategoryTreeAsync(ct)));

        group.MapGet("/collections", async (CatalogQueries queries, CancellationToken ct) =>
            TypedResults.Ok(await queries.GetCollectionsAsync(ct)));

        group.MapGet("/products", async ([AsParameters] ListProductsRequest request, CatalogQueries queries, CancellationToken ct) =>
                (await queries.ListProductsAsync(request, ct)).ToHttp())
            .Validate<ListProductsRequest>()
            .Produces<ProductListPage>();

        group.MapGet("/facets", async Task<Ok<CatalogFacets>> (string? category, string? collection, CatalogQueries queries, CancellationToken ct) =>
            TypedResults.Ok(await queries.GetFacetsAsync(category, collection, ct)));

        group.MapGet("/products/{slug}", async (string slug, CatalogQueries queries, CancellationToken ct) =>
                (await queries.GetProductAsync(slug, ct)).ToHttp())
            .Produces<ProductDetail>();

        group.MapGet("/sitemap", async (CatalogQueries queries, CancellationToken ct) =>
            TypedResults.Ok(await queries.GetSitemapAsync(ct)));
    }
}
