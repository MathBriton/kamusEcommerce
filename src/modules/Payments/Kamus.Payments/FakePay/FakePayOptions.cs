namespace Kamus.Payments.FakePay;

internal sealed class FakePayOptions
{
    public const string SectionName = "Payments:FakePay";

    /// <summary>Endereço para onde o FakePay envia os webhooks (a própria API, em /api/payments/webhooks/fakepay).</summary>
    public string WebhookUrl { get; set; } = "http://localhost:5080/api/payments/webhooks/fakepay";

    /// <summary>Segredo compartilhado usado para assinar (HMAC-SHA256) os webhooks.</summary>
    public string WebhookSecret { get; set; } = "whsec_dev_kamus_fakepay";

    /// <summary>Tempo que o "banco" leva para responder a uma cobrança.</summary>
    public TimeSpan ProcessingDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Tolerância de relógio para aceitar a assinatura de um webhook.</summary>
    public TimeSpan SignatureTolerance { get; set; } = TimeSpan.FromMinutes(5);
}
