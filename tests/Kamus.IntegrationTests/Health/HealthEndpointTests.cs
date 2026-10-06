using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infrastructure;

namespace Kamus.IntegrationTests.Health;

public sealed class HealthEndpointTests(KamusApiFactory factory)
{
    [Fact]
    public async Task Health_retorna_healthy_com_postgres_e_redis()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<HealthPayload>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be("Healthy");
        body.Checks.Select(c => c.Name).Should().BeEquivalentTo("postgres", "redis");
    }

    private sealed record HealthPayload(string Status, IReadOnlyList<HealthCheckPayload> Checks);

    private sealed record HealthCheckPayload(string Name, string Status);
}
