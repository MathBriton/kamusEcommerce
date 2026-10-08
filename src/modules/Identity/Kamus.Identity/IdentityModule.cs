using FluentValidation;
using Kamus.Identity.Api;
using Kamus.Identity.Contracts;
using Kamus.Identity.Domain;
using Kamus.Identity.Persistence;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Modules;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Identity;

public sealed class IdentityModule : IModule
{
    public const string AuthCookieName = "kamus_auth";

    public string Name => "identity";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<KamusIdentityDbContext>(KamusIdentityDbContext.Schema);

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminAccess.Policy, policy => policy.RequireRole(AdminAccess.Role));

        services.AddIdentityCore<KamusUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddClaimsPrincipalFactory<KamusClaimsPrincipalFactory>()
            .AddEntityFrameworkStores<KamusIdentityDbContext>()
            .AddSignInManager()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true;
            // Lax impede o envio do cookie em POSTs vindos de outros sites (proteção contra CSRF).
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            // É uma API: responde 401/403 em vez de redirecionar para uma tela de login.
            options.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddDataProtection()
            .SetApplicationName("kamus")
            .PersistKeysToDbContext<KamusIdentityDbContext>();

        services.AddScoped<ICustomerDirectory, CustomerDirectory>();
        services.Configure<AdminUserOptions>(configuration.GetSection(AdminUserOptions.SectionName));
        services.AddScoped<IDataSeeder, AdminUserSeeder>();
        services.AddValidatorsFromAssemblyContaining<IdentityModule>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => IdentityEndpoints.Map(endpoints);
}
