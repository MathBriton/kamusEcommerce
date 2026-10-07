using System.Net;
using System.Net.Http.Json;
using Kamus.Identity.Api;
using Kamus.IntegrationTests.Infrastructure;

namespace Kamus.IntegrationTests.Admin;

public sealed class AdminAccessTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    public static TheoryData<string, string> AdminRoutes => new()
    {
        { "GET", "/api/admin/catalog/products" },
        { "POST", "/api/admin/catalog/products" },
        { "PUT", $"/api/admin/inventory/skus/{Guid.NewGuid()}" },
        { "GET", "/api/admin/orders" },
        { "POST", $"/api/admin/orders/{Guid.NewGuid()}/ship" },
        { "GET", "/api/admin/reports/overview" },
    };

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Anonimo_recebe_401_e_cliente_recebe_403(string method, string url)
    {
        var anonymous = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Ct);
        var customer = await (await _shop.NewCustomerAsync()).SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Ct);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        customer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Administrador_e_identificado_na_sessao()
    {
        var admin = await _shop.AdminAsync();

        var session = await admin.GetFromJsonAsync<SessionResponse>("/api/identity/session", Ct);

        session!.User!.IsAdmin.Should().BeTrue();
        (await admin.GetAsync("/api/admin/reports/overview", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
