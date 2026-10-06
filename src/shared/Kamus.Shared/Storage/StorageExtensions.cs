using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Shared.Storage;

public static class StorageExtensions
{
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LocalDiskStorageOptions>(configuration.GetSection(LocalDiskStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
        return services;
    }

    /// <summary>Serve os arquivos do <see cref="IFileStorage"/> em <c>/files/{key}</c>.</summary>
    public static IEndpointConventionBuilder MapFileStorage(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/files/{**key}", async Task<IResult> (string key, IFileStorage storage, HttpContext http, CancellationToken ct) =>
        {
            StoredFile? file;
            try
            {
                file = await storage.OpenReadAsync(key, ct);
            }
            catch (ArgumentException)
            {
                return TypedResults.NotFound();
            }

            if (file is null)
            {
                return TypedResults.NotFound();
            }

            http.Response.Headers.CacheControl = "public, max-age=86400";
            return TypedResults.Stream(file.Content, file.ContentType, lastModified: file.LastModified);
        })
        .ExcludeFromDescription();
}
