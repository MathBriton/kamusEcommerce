using System.Net;
using System.Net.Http.Json;
using Kamus.Identity.Api;
using Kamus.IntegrationTests.Infrastructure;

namespace Kamus.IntegrationTests.Identity;

public sealed class IdentityTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadastro_login_e_logout()
    {
        var email = $"ana-{Guid.NewGuid():N}@kamus.test";
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/identity/register", new { email, password = "senha-forte-1", fullName = "Ana Souza" }, Ct);
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        register.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("kamus_auth=") && c.Contains("httponly"));

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/identity/session", Ct);
        session!.Authenticated.Should().BeTrue();

        var me = await client.GetFromJsonAsync<MeResponse>("/api/identity/me", Ct);
        me!.Email.Should().Be(email);
        me.FullName.Should().Be("Ana Souza");

        (await client.PostAsync("/api/identity/logout", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/identity/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetFromJsonAsync<SessionResponse>("/api/identity/session", Ct))!.Authenticated.Should().BeFalse();

        var other = factory.CreateClient();
        (await other.PostAsJsonAsync("/api/identity/login", new { email, password = "errada-123" }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await other.PostAsJsonAsync("/api/identity/login", new { email, password = "senha-forte-1" }, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.GetAsync("/api/identity/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Email_duplicado_retorna_409()
    {
        var email = $"dup-{Guid.NewGuid():N}@kamus.test";
        var payload = new { email, password = "senha-forte-1", fullName = "Duplicado" };

        (await factory.CreateClient().PostAsJsonAsync("/api/identity/register", payload, Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await factory.CreateClient().PostAsJsonAsync("/api/identity/register", payload, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("nao-e-email", "senha-forte-1", "Nome Válido")]
    [InlineData("ok@kamus.test", "curta", "Nome Válido")]
    [InlineData("ok@kamus.test", "senha-forte-1", "")]
    public async Task Cadastro_invalido_retorna_400(string email, string password, string fullName)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/identity/register", new { email, password, fullName }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
