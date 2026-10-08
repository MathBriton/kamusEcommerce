using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Kamus.Audit.Api;
using Kamus.Identity.Domain;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Shared.Auditing;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Audit;

public sealed class AuditEndpointsTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Shop _shop = new(factory);

    public static TheoryData<string> AuditRoutes => new()
    {
        "/api/admin/audit/entries",
        "/api/admin/audit/entries.csv",
        "/api/admin/audit/actors",
        $"/api/admin/audit/subjects/Product/{Guid.NewGuid()}",
    };

    [Theory]
    [MemberData(nameof(AuditRoutes))]
    public async Task Rotas_da_auditoria_exigem_Admin(string url)
    {
        var anonymous = await factory.CreateClient().GetAsync(url, Ct);
        var customer = await (await _shop.NewCustomerAsync()).GetAsync(url, Ct);
        var admin = await (await _shop.AdminAsync()).GetAsync(url, Ct);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        customer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        admin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lista_paginada_mais_recentes_primeiro_com_filtros()
    {
        var module = AuditSeed.NewModule();
        var maria = new AuditActor(AuditActorKind.Admin, Guid.CreateVersion7(), "Maria Silva", "maria@kamus.test");
        var fakePay = AuditActor.System($"FakePay {module}");
        var day = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        await AuditSeed.WriteAsync(factory,
            AuditSeed.Record(module, day.AddDays(-1), maria, "created", changes: [new AuditChange("Nome", null, "Camisa")]),
            AuditSeed.Record(module, day, maria, "published", changes: [new AuditChange("Situação", "Rascunho", "Publicado")]),
            AuditSeed.Record(module, day.AddHours(11), fakePay, "paid", entityType: "Order"),
            AuditSeed.Record(module, day.AddDays(1), fakePay, "shipped", entityType: "Order"));
        var admin = await _shop.AdminAsync();

        var page = await admin.GetFromJsonAsync<AuditEntriesPage>($"/api/admin/audit/entries?module={module}&pageSize=3", Ct);
        page!.Total.Should().Be(4);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(3);
        page.Items.Select(i => i.Action).Should().Equal("shipped", "paid", "published");
        page.Items[2].Changes.Should().Equal(new AuditChange("Situação", "Rascunho", "Publicado"));
        page.Items[2].Actor.Should().Be(new AuditActorDto("Admin", maria.UserId, "Maria Silva", "maria@kamus.test"));
        page.Items[2].IpAddress.Should().Be("10.0.0.7");

        var second = await admin.GetFromJsonAsync<AuditEntriesPage>($"/api/admin/audit/entries?module={module}&pageSize=3&page=2", Ct);
        second!.Items.Select(i => i.Action).Should().Equal("created");

        var byUser = await admin.GetFromJsonAsync<AuditEntriesPage>($"/api/admin/audit/entries?module={module}&actor={maria.UserId}", Ct);
        byUser!.Items.Select(i => i.Action).Should().Equal("published", "created");

        var bySystem = await admin.GetFromJsonAsync<AuditEntriesPage>($"/api/admin/audit/entries?module={module}&actor={Uri.EscapeDataString($"system:{fakePay.Name}")}", Ct);
        bySystem!.Items.Select(i => i.Action).Should().Equal("shipped", "paid");

        var byAction = await admin.GetFromJsonAsync<AuditEntriesPage>($"/api/admin/audit/entries?module={module}&action=paid", Ct);
        byAction!.Total.Should().Be(1);

        var byDay = await admin.GetFromJsonAsync<AuditEntriesPage>($"/api/admin/audit/entries?module={module}&from=2026-03-10&to=2026-03-10", Ct);
        byDay!.Items.Select(i => i.Action).Should().Equal("paid", "published");
    }

    [Theory]
    [InlineData("actor=nao-e-usuario")]
    [InlineData("from=2026-03-10&to=2026-03-01")]
    public async Task Filtro_invalido_retorna_400(string query)
    {
        var admin = await _shop.AdminAsync();

        (await admin.GetAsync($"/api/admin/audit/entries?{query}", Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Exporta_csv_com_bom_e_formulas_neutralizadas()
    {
        var module = AuditSeed.NewModule();
        var actor = new AuditActor(AuditActorKind.Admin, Guid.CreateVersion7(), "=cmd|' /C calc'!A0", null);
        await AuditSeed.WriteAsync(factory, AuditSeed.Record(module, DateTimeOffset.UtcNow, actor, label: "@SUM(1+1)"));
        var admin = await _shop.AdminAsync();

        var response = await admin.GetAsync($"/api/admin/audit/entries.csv?module={module}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.ToString().Should().Be("text/csv; charset=utf-8");
        response.Content.Headers.ContentDisposition!.FileName.Should().StartWith("auditoria-");
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        bytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        var lines = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("Data e hora (UTC);Módulo;Ação;Item");
        lines[1].Should().Contain(";'@SUM(1+1);").And.Contain(";'=cmd|' /C calc'!A0;");
    }

    [Fact]
    public async Task Lista_os_atores_com_chave_por_usuario_ou_sistema()
    {
        var module = AuditSeed.NewModule();
        var userId = Guid.CreateVersion7();
        var system = AuditActor.System($"Robô {module}");
        await AuditSeed.WriteAsync(factory,
            AuditSeed.Record(module, DateTimeOffset.UtcNow.AddMinutes(-5), new AuditActor(AuditActorKind.Customer, userId, $"Nome antigo {module}", "a@kamus.test")),
            AuditSeed.Record(module, DateTimeOffset.UtcNow, new AuditActor(AuditActorKind.Customer, userId, $"Nome novo {module}", "b@kamus.test")),
            AuditSeed.Record(module, DateTimeOffset.UtcNow, system));
        var admin = await _shop.AdminAsync();

        var actors = await admin.GetFromJsonAsync<List<AuditActorOptionDto>>("/api/admin/audit/actors", Ct);

        actors.Should().ContainSingle(a => a.Key == userId.ToString())
            .Which.Should().Be(new AuditActorOptionDto(userId.ToString(), "Customer", $"Nome novo {module}", "b@kamus.test"));
        actors.Should().ContainSingle(a => a.Key == $"system:{system.Name}")
            .Which.Kind.Should().Be("System");
        var byName = Comparer<string>.Create((a, b) =>
            CultureInfo.InvariantCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
        actors!.Select(a => a.Name).Should().BeInAscendingOrder(byName);
    }

    [Fact]
    public async Task Historico_do_agregado_mais_recente_primeiro()
    {
        var module = AuditSeed.NewModule();
        var productId = Guid.CreateVersion7();
        var actor = AuditActor.System("Teste");
        await AuditSeed.WriteAsync(factory,
            AuditSeed.Record(module, DateTimeOffset.UtcNow.AddMinutes(-2), actor, "created", subjectId: productId),
            AuditSeed.Record(module, DateTimeOffset.UtcNow.AddMinutes(-1), actor, "updated", subjectId: productId),
            AuditSeed.Record(module, DateTimeOffset.UtcNow, actor, "deleted", subjectId: productId),
            AuditSeed.Record(module, DateTimeOffset.UtcNow, actor, "created"));
        var admin = await _shop.AdminAsync();

        var history = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Product/{productId}", Ct);
        var limited = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/admin/audit/subjects/Product/{productId}?limit=1", Ct);

        history!.Select(h => h.Action).Should().Equal("deleted", "updated", "created");
        limited!.Select(h => h.Action).Should().Equal("deleted");
    }

    [Fact]
    public async Task Cookie_traz_o_nome_completo_para_a_auditoria()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<KamusUser>>();
        var principals = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<KamusUser>>();

        var admin = await users.FindByEmailAsync("admin@kamus.test");
        var principal = await principals.CreateAsync(admin!);

        principal.GetFullName().Should().Be("Administrador Kamus");
        principal.IsInRole(AdminAccess.Role).Should().BeTrue();
    }
}
