namespace Kamus.Orders.Application;

internal sealed class OrdersOptions
{
    public const string SectionName = "Orders";

    /// <summary>Quanto tempo o estoque fica reservado esperando o pagamento.</summary>
    public TimeSpan PaymentTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Habilita endpoints que simulam envio/entrega (não há painel administrativo no MVP).</summary>
    public bool EnableFulfillmentSimulation { get; set; }
}
