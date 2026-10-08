using System.Security.Claims;
using Kamus.Identity.Domain;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kamus.Identity;

/// <summary>
/// Acrescenta ao cookie a claim <see cref="KamusClaims.FullName"/>: é o nome que a auditoria grava
/// como ator, sem precisar consultar o módulo Identity a cada alteração.
/// </summary>
internal sealed class KamusClaimsPrincipalFactory(
    UserManager<KamusUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<KamusUser, IdentityRole<Guid>>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(KamusUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            identity.AddClaim(new Claim(KamusClaims.FullName, user.FullName));
        }

        return identity;
    }
}
