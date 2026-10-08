using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.Catalog.Api;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Admin;
using Kamus.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Catalog;

/// <summary>
/// Monta, pelo backoffice (HTTP), produtos para os testes da lixeira e da auditoria do catálogo.
/// Cada instância usa uma categoria própria: a vitrine filtrada por ela só mostra os produtos do teste.
/// </summary>
internal sealed class TrashFixtures(KamusApiFactory factory)
{
    // Assinatura mínima de PNG seguida de bytes quaisquer: o servidor só olha o cabeçalho.
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3, 4];

    private readonly Shop _shop = new(factory);

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Caminho da categoria exclusiva deste teste.</summary>
    public string Category { get; } = $"t{Guid.NewGuid():N}"[..12] + "/lixeira";

    public Task<HttpClient> AdminAsync() => _shop.AdminAsync();

    public Task<HttpClient> CustomerAsync() => _shop.NewCustomerAsync();

    public HttpClient Store() => factory.CreateClient();

    public async Task<Guid> CategoryIdAsync()
    {
        await new FixedCategories(factory).EnsureAsync(Category);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await db.Categories.Where(c => c.Path == Category).Select(c => c.Id).SingleAsync(Ct);
    }

    public async Task<AdminProductDetail> CreateDraftAsync(HttpClient admin, string name, string brand = "Kamus Studio")
    {
        var response = await admin.PostAsJsonAsync("/api/admin/catalog/products",
            new SaveProductRequest(name, $"Descrição de {name}.", brand, await CategoryIdAsync(), null), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
    }

    public static async Task<AdminProductDetail> AddSkusAsync(HttpClient admin, Guid productId, string color, string[] sizes, decimal price = 129.90m, int stock = 4)
    {
        var response = await admin.PostAsJsonAsync($"/api/admin/catalog/products/{productId}/skus",
            new AddSkusRequest(color, "#1f1f1f", sizes, price, null, stock), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
    }

    public static async Task<AdminProductDetail> UploadImageAsync(HttpClient admin, Guid productId, string color)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "foto.png");
        form.Add(new StringContent(color), "color");
        var response = await admin.PostAsync($"/api/admin/catalog/products/{productId}/images", form, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
    }

    public static async Task<AdminProductDetail> PublishAsync(HttpClient admin, Guid productId)
    {
        var response = await admin.PostAsync($"/api/admin/catalog/products/{productId}/publish", null, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<AdminProductDetail>(Ct))!;
    }

    /// <summary>Produto publicado com dois SKUs (Preto P e M) e uma imagem preta.</summary>
    public async Task<AdminProductDetail> CreatePublishedAsync(HttpClient admin, string name)
    {
        var product = await CreateDraftAsync(admin, name);
        await AddSkusAsync(admin, product.Id, "Preto", ["P", "M"]);
        await UploadImageAsync(admin, product.Id, "Preto");
        return await PublishAsync(admin, product.Id);
    }

    public static async Task<TrashPage> TrashAsync(HttpClient admin, string type = "all")
    {
        var response = await admin.GetAsync($"/api/admin/catalog/trash?type={type}&page=1&pageSize=100", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<TrashPage>(Ct))!;
    }

    public static async Task<HttpResponseMessage> RestoreAsync(HttpClient admin, string type, Guid id) =>
        await admin.PostAsync($"/api/admin/catalog/trash/{type}/{id}/restore", null, Ct);

    public static async Task<string> RestoreOkAsync(HttpClient admin, string type, Guid id)
    {
        var response = await RestoreAsync(admin, type, id);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<RestoreResult>(Ct))!.Message;
    }

    public static async Task DeleteProductAsync(HttpClient admin, Guid productId)
    {
        var response = await admin.DeleteAsync($"/api/admin/catalog/products/{productId}", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Lê o <c>detail</c> (mensagem em português) e o <c>code</c> de um problem+json.</summary>
    public static async Task<(string? Code, string? Detail)> ProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Ct);
        return (problem!.Extensions.TryGetValue("code", out var code) ? code?.ToString() : null, problem.Detail);
    }
}
