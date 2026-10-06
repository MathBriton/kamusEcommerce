using Microsoft.AspNetCore.Identity;

namespace Kamus.Identity.Domain;

internal sealed class KamusUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
