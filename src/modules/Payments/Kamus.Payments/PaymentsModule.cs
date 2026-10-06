using Kamus.Payments.Api;
using Kamus.Payments.Application;
using Kamus.Payments.Contracts;
using Kamus.Payments.FakePay;
using Kamus.Payments.Persistence;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Payments;

public sealed class PaymentsModule : IModule
{
    public string Name => "payments";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PaymentsDbContext>(PaymentsDbContext.Schema);
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<WebhookProcessor>();
        services.AddScoped<PaymentResultNotifier>();
        services.AddHostedService<PaymentNotificationDispatcher>();

        // FakePay: provedor externo simulado, rodando no mesmo processo por conveniência.
        services.Configure<FakePayOptions>(configuration.GetSection(FakePayOptions.SectionName));
        services.AddSingleton<FakePayProvider>();
        services.AddSingleton<IFakePayApi>(sp => sp.GetRequiredService<FakePayProvider>());
        services.AddHostedService<FakePayProcessor>();
        services.AddHttpClient(FakePayProcessor.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => WebhookEndpoints.Map(endpoints);
}
