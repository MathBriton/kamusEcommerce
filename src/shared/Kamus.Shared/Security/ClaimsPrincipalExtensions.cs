using System.Security.Claims;

namespace Kamus.Shared.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id do cliente autenticado (claim NameIdentifier emitida pelo módulo Identity).</summary>
    public static Guid? GetCustomerId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Guid GetRequiredCustomerId(this ClaimsPrincipal principal) =>
        principal.GetCustomerId() ?? throw new InvalidOperationException("Usuário não autenticado.");
}
