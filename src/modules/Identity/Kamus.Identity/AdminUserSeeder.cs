using Kamus.Identity.Domain;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Identity;

internal sealed class AdminUserOptions
{
    public const string SectionName = "Admin";

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string FullName { get; set; } = "Administrador Kamus";
}

/// <summary>
/// Garante o papel Admin e, se configurado, um usuário administrador. Roda em todo ambiente
/// (não depende do seed de catálogo): é assim que a primeira pessoa entra no backoffice.
/// </summary>
internal sealed class AdminUserSeeder(
    RoleManager<IdentityRole<Guid>> roles,
    UserManager<KamusUser> users,
    IOptions<AdminUserOptions> options,
    TimeProvider clock,
    ILogger<AdminUserSeeder> logger) : IDataSeeder
{
    public int Order => 10;

    public bool IsSampleData => false;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await roles.RoleExistsAsync(AdminAccess.Role))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(AdminAccess.Role));
        }

        var (email, password) = (options.Value.Email, options.Value.Password);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new KamusUser { Email = email, UserName = email, FullName = options.Value.FullName, CreatedAt = clock.GetUtcNow() };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError("Não foi possível criar o administrador: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Administrador {Email} criado", email);
        }

        if (!await users.IsInRoleAsync(user, AdminAccess.Role))
        {
            await users.AddToRoleAsync(user, AdminAccess.Role);
        }
    }
}
