using System.Security.Claims;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Http;

namespace Kamus.Shared.Auditing;

internal sealed class CurrentActor(IHttpContextAccessor http) : ICurrentActor
{
    private readonly Stack<AuditActor> _overrides = new();
    private int _suppressions;

    public AuditActor Actor => _overrides.Count > 0 ? _overrides.Peek() : FromUser(http.HttpContext?.User);

    public bool IsAuditingSuppressed => _suppressions > 0;

    public string? CorrelationId => http.HttpContext?.TraceIdentifier;

    public string? IpAddress
    {
        get
        {
            var address = http.HttpContext?.Connection.RemoteIpAddress;
            if (address is null)
            {
                return null;
            }

            return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
        }
    }

    public IDisposable ActAs(AuditActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        _overrides.Push(actor);
        return new Scope(() => _overrides.Pop());
    }

    public IDisposable SuppressAuditing()
    {
        _suppressions++;
        return new Scope(() => _suppressions--);
    }

    /// <summary>Monta o ator a partir das claims emitidas pelo módulo Identity.</summary>
    internal static AuditActor FromUser(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return AuditActor.System(AuditActor.DefaultSystemName);
        }

        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(ClaimTypes.Name);
        var name = user.GetFullName() ?? email ?? AuditActor.DefaultSystemName;
        var kind = user.IsInRole(AdminAccess.Role) ? AuditActorKind.Admin : AuditActorKind.Customer;
        return new AuditActor(kind, user.GetCustomerId(), name, email);
    }

    /// <summary>Restaura o estado anterior uma única vez, mesmo com Dispose repetido.</summary>
    private sealed class Scope(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}
