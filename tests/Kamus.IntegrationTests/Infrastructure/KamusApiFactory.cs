using Kamus.Payments.FakePay;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Kamus.IntegrationTests.Infrastructure.KamusApiFactory))]

namespace Kamus.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe a API inteira contra Postgres e Redis reais (Testcontainers).
/// Compartilhada por todos os testes do assembly.
/// </summary>
public sealed class KamusApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("kamus")
        .WithUsername("kamus")
        .WithPassword("kamus")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public string RedisConnectionString => _redis.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Sobe o host (e roda as migrations) uma única vez, antes de os testes rodarem em paralelo.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionString);

        builder.ConfigureTestServices(services =>
        {
            // Os webhooks do FakePay entram direto no TestServer, sem rede.
            services.AddHttpClient(FakePayProcessor.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(sp => ((TestServer)sp.GetRequiredService<IServer>()).CreateHandler());
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
