using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Shared.Security;

/// <summary>Convenções do backoffice: papel, policy e prefixo das rotas administrativas.</summary>
public static class AdminAccess
{
    public const string Role = "Admin";

    public const string Policy = "Admin";

    /// <summary>Grupo <c>/api/admin/{area}</c> que exige o papel Admin.</summary>
    public static RouteGroupBuilder MapAdminGroup(this IEndpointRouteBuilder endpoints, string area) =>
        endpoints.MapGroup($"/api/admin/{area}")
            .RequireAuthorization(Policy)
            .WithTags($"Admin · {area}");
}
