using System.Text.Json;
using Kamus.Payments.Application;
using Kamus.Payments.FakePay;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Kamus.Payments.Api;

internal static class WebhookEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/payments/webhooks/fakepay", ReceiveAsync)
            .WithTags("Payments")
            .ExcludeFromDescription();
    }

    private static async Task<IResult> ReceiveAsync(
        HttpRequest request,
        WebhookProcessor processor,
        IOptions<FakePayOptions> options,
        TimeProvider clock,
        CancellationToken ct)
    {
        // A assinatura é calculada sobre o corpo exato, então ele é lido como texto bruto.
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(ct);

        var signature = request.Headers[FakePaySignature.Header].ToString();
        if (!FakePaySignature.Verify(body, signature, options.Value.WebhookSecret, clock.GetUtcNow(), options.Value.SignatureTolerance))
        {
            return TypedResults.Unauthorized();
        }

        FakePayWebhookEvent? @event;
        try
        {
            @event = FakePayJson.Parse(body);
        }
        catch (JsonException)
        {
            return TypedResults.BadRequest();
        }

        if (@event is null)
        {
            return TypedResults.BadRequest();
        }

        return await processor.ProcessAsync(@event, ct) switch
        {
            WebhookOutcome.UnknownPayment => TypedResults.NotFound(),
            var outcome => TypedResults.Ok(new { received = true, duplicate = outcome == WebhookOutcome.Duplicate }),
        };
    }
}
