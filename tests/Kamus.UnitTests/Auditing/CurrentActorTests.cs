using System.Net;
using System.Security.Claims;
using Kamus.Shared.Auditing;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Http;

namespace Kamus.UnitTests.Auditing;

public sealed class CurrentActorTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    private static CurrentActor For(ClaimsPrincipal? user, string? ip = null)
    {
        var http = new DefaultHttpContext { TraceIdentifier = "req-1" };
        if (user is not null)
        {
            http.User = user;
        }

        if (ip is not null)
        {
            http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        }

        return new CurrentActor(new HttpContextAccessor { HttpContext = http });
    }

    private static ClaimsPrincipal User(string? fullName, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId.ToString()),
            new(ClaimTypes.Name, "maria@kamus.test"),
            new(ClaimTypes.Email, "maria@kamus.test"),
        };
        if (fullName is not null)
        {
            claims.Add(new Claim(KamusClaims.FullName, fullName));
        }

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste"));
    }

    [Fact]
    public void Cliente_autenticado_usa_o_nome_completo()
    {
        var actor = For(User("Maria Silva")).Actor;

        actor.Should().Be(new AuditActor(AuditActorKind.Customer, UserId, "Maria Silva", "maria@kamus.test"));
    }

    [Fact]
    public void Administrador_e_identificado_pelo_papel_e_sem_claim_de_nome_usa_o_email()
    {
        var actor = For(User(null, AdminAccess.Role)).Actor;

        actor.Kind.Should().Be(AuditActorKind.Admin);
        actor.Name.Should().Be("maria@kamus.test");
    }

    [Fact]
    public void Sem_usuario_o_ator_e_o_sistema()
    {
        var current = For(new ClaimsPrincipal(new ClaimsIdentity()), "::ffff:10.0.0.7");

        current.Actor.Should().Be(AuditActor.System("Sistema"));
        current.CorrelationId.Should().Be("req-1");
        current.IpAddress.Should().Be("10.0.0.7");
    }

    [Fact]
    public void ActAs_e_aninhavel_e_restaura_o_anterior()
    {
        var current = For(User("Maria Silva"));

        using (current.ActAs(AuditActor.System("FakePay")))
        {
            using (current.ActAs(AuditActor.System("Expurgo automático")))
            {
                current.Actor.Name.Should().Be("Expurgo automático");
            }

            current.Actor.Name.Should().Be("FakePay");
        }

        current.Actor.Name.Should().Be("Maria Silva");
    }

    [Fact]
    public void Supressao_e_aninhavel()
    {
        var current = For(null);

        var outer = current.SuppressAuditing();
        var inner = current.SuppressAuditing();
        inner.Dispose();
        inner.Dispose(); // idempotente
        current.IsAuditingSuppressed.Should().BeTrue();

        outer.Dispose();
        current.IsAuditingSuppressed.Should().BeFalse();
    }
}
