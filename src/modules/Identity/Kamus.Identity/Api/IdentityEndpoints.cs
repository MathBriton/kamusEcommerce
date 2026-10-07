using FluentValidation;
using Kamus.Identity.Contracts;
using Kamus.Identity.Domain;
using Kamus.Shared.Events;
using Kamus.Shared.Results;
using Kamus.Shared.Security;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Identity.Api;

public sealed record RegisterRequest(string Email, string Password, string FullName);

public sealed record LoginRequest(string Email, string Password);

public sealed record MeResponse(Guid Id, string Email, string FullName, bool IsAdmin = false);

/// <summary>Estado da sessão sem erro 401: usado pelo cabeçalho do site para visitantes anônimos.</summary>
public sealed record SessionResponse(bool Authenticated, MeResponse? User);

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(r => r.FullName).NotEmpty().MinimumLength(3).MaximumLength(150);
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
    }
}

internal static class IdentityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity").WithTags("Identity");

        group.MapPost("/register", RegisterAsync).Validate<RegisterRequest>();
        group.MapPost("/login", LoginAsync).Validate<LoginRequest>();
        group.MapPost("/logout", async (SignInManager<KamusUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return TypedResults.NoContent();
        });
        group.MapGet("/me", MeAsync).RequireAuthorization();
        group.MapGet("/session", SessionAsync);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<KamusUser> users,
        SignInManager<KamusUser> signIn,
        IEventPublisher events,
        TimeProvider clock,
        CancellationToken ct)
    {
        var user = new KamusUser
        {
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return Error.Conflict("identity.email_taken", "Já existe uma conta com este e-mail.").ToProblem();
            }

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = [.. result.Errors.Select(e => e.Description)],
            });
        }

        await signIn.SignInAsync(user, isPersistent: true);
        await events.PublishAsync(new CustomerSignedIn(user.Id), ct);

        return TypedResults.Created("/api/identity/me", new MeResponse(user.Id, user.Email, user.FullName));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<KamusUser> users,
        SignInManager<KamusUser> signIn,
        IEventPublisher events,
        CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Error.Unauthorized("identity.locked_out", "Muitas tentativas. Tente novamente em alguns minutos.").ToProblem();
        }

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        await events.PublishAsync(new CustomerSignedIn(user.Id), ct);
        return TypedResults.Ok(new MeResponse(user.Id, user.Email!, user.FullName, await users.IsInRoleAsync(user, AdminAccess.Role)));
    }

    private static async Task<IResult> MeAsync(HttpContext http, UserManager<KamusUser> users)
    {
        var user = await users.FindByIdAsync(http.User.GetRequiredCustomerId().ToString());
        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new MeResponse(user.Id, user.Email!, user.FullName, http.User.IsInRole(AdminAccess.Role)));
    }

    private static async Task<IResult> SessionAsync(HttpContext http, UserManager<KamusUser> users)
    {
        var id = http.User.GetCustomerId();
        var user = id is null ? null : await users.FindByIdAsync(id.Value.ToString());
        return TypedResults.Ok(user is null
            ? new SessionResponse(false, null)
            : new SessionResponse(true, new MeResponse(user.Id, user.Email!, user.FullName, http.User.IsInRole(AdminAccess.Role))));
    }

    // Mesma mensagem para e-mail inexistente e senha errada: não revela quais e-mails têm conta.
    private static IResult InvalidCredentials() =>
        Error.Unauthorized("identity.invalid_credentials", "E-mail ou senha inválidos.").ToProblem();
}
