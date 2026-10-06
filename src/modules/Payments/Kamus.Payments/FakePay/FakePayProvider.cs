using System.Text.Json;
using System.Threading.Channels;
using Kamus.Payments.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Payments.FakePay;

/// <summary>
/// Simula um provedor de pagamento EXTERNO. A loja só conhece <see cref="IFakePayApi"/> (como seria
/// um SDK HTTP); o resultado chega depois, de forma assíncrona, via webhook assinado.
/// Cenários escolhidos pelo número do cartão (ver <see cref="FakePayTestCards"/>).
/// </summary>
internal interface IFakePayApi
{
    Task<string> CreateChargeAsync(Guid reference, decimal amount, string cardNumber, CancellationToken cancellationToken);
}

internal enum FakePayScenario
{
    Approve,
    Decline,
    Timeout,
    DuplicateWebhook,
}

internal sealed record FakePayCharge(string ChargeId, Guid Reference, decimal Amount, FakePayScenario Scenario);

internal sealed class FakePayProvider : IFakePayApi
{
    private readonly Channel<FakePayCharge> _queue = Channel.CreateUnbounded<FakePayCharge>();

    public ChannelReader<FakePayCharge> Pending => _queue.Reader;

    public async Task<string> CreateChargeAsync(Guid reference, decimal amount, string cardNumber, CancellationToken cancellationToken)
    {
        var scenario = cardNumber switch
        {
            FakePayTestCards.Declined => FakePayScenario.Decline,
            FakePayTestCards.Timeout => FakePayScenario.Timeout,
            FakePayTestCards.DuplicateWebhook => FakePayScenario.DuplicateWebhook,
            _ => FakePayScenario.Approve,
        };

        var charge = new FakePayCharge($"ch_{Guid.NewGuid():N}", reference, amount, scenario);
        await _queue.Writer.WriteAsync(charge, cancellationToken);
        return charge.ChargeId;
    }
}

/// <summary>"Servidor" do FakePay: processa as cobranças e dispara os webhooks.</summary>
internal sealed class FakePayProcessor(
    FakePayProvider provider,
    IHttpClientFactory httpClients,
    TimeProvider clock,
    IOptions<FakePayOptions> options,
    ILogger<FakePayProcessor> logger) : BackgroundService
{
    public const string HttpClientName = "fakepay-webhooks";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var charge in provider.Pending.ReadAllAsync(stoppingToken))
        {
            // Cada cobrança é processada em paralelo, como num provedor real.
            _ = Task.Run(() => ProcessAsync(charge, stoppingToken), stoppingToken);
        }
    }

    private async Task ProcessAsync(FakePayCharge charge, CancellationToken ct)
    {
        try
        {
            await Task.Delay(options.Value.ProcessingDelay, clock, ct);

            if (charge.Scenario == FakePayScenario.Timeout)
            {
                logger.LogInformation("FakePay: cobrança {ChargeId} sem resposta (cenário timeout)", charge.ChargeId);
                return;
            }

            var approved = charge.Scenario != FakePayScenario.Decline;
            var @event = new FakePayWebhookEvent(
                $"evt_{Guid.NewGuid():N}",
                approved ? FakePayWebhookEvent.ChargeSucceeded : FakePayWebhookEvent.ChargeFailed,
                clock.GetUtcNow().ToUnixTimeSeconds(),
                new FakePayChargeData(charge.ChargeId, charge.Reference, charge.Amount, approved ? null : "Transação não autorizada pelo emissor"));

            await SendAsync(@event, ct);

            if (charge.Scenario == FakePayScenario.DuplicateWebhook)
            {
                // Entrega "pelo menos uma vez": o mesmo evento chega de novo, inclusive em paralelo.
                await Task.WhenAll(SendAsync(@event, ct), SendAsync(@event, ct));
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "FakePay: falha ao processar a cobrança {ChargeId}", charge.ChargeId);
        }
    }

    private async Task SendAsync(FakePayWebhookEvent @event, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(@event, Json);
        var client = httpClients.CreateClient(HttpClientName);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.WebhookUrl)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Add(FakePaySignature.Header, FakePaySignature.Sign(body, options.Value.WebhookSecret, clock.GetUtcNow()));

            try
            {
                using var response = await client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                logger.LogWarning("FakePay: webhook {EventId} recusado com {Status} (tentativa {Attempt})", @event.Id, (int)response.StatusCode, attempt);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "FakePay: falha ao entregar webhook {EventId} (tentativa {Attempt})", @event.Id, attempt);
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt), clock, ct);
        }
    }
}

internal static class FakePayJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static FakePayWebhookEvent? Parse(string body) => JsonSerializer.Deserialize<FakePayWebhookEvent>(body, Options);
}
