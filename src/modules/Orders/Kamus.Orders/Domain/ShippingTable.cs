namespace Kamus.Orders.Domain;

/// <summary>Frete por tabela fixa de região (sem integração com transportadora no MVP).</summary>
public static class ShippingTable
{
    public const decimal FreeShippingThreshold = 399m;

    private static readonly (string Region, decimal Cost, int Days, string[] States)[] Regions =
    [
        ("Sudeste", 19.90m, 3, ["SP", "RJ", "MG", "ES"]),
        ("Sul", 24.90m, 5, ["PR", "SC", "RS"]),
        ("Centro-Oeste", 29.90m, 6, ["DF", "GO", "MT", "MS"]),
        ("Nordeste", 34.90m, 8, ["BA", "SE", "AL", "PE", "PB", "RN", "CE", "PI", "MA"]),
        ("Norte", 39.90m, 10, ["PA", "AM", "AP", "RR", "RO", "AC", "TO"]),
    ];

    public static IReadOnlyList<string> States { get; } = [.. Regions.SelectMany(r => r.States).Order()];

    public static bool IsValidState(string? state) => state is not null && States.Contains(state.ToUpperInvariant());

    internal static ShippingInfo? Quote(string state, decimal subtotal)
    {
        var region = Regions.FirstOrDefault(r => r.States.Contains(state.ToUpperInvariant()));
        if (region.Region is null)
        {
            return null;
        }

        var cost = subtotal >= FreeShippingThreshold ? 0m : region.Cost;
        return new ShippingInfo(region.Region, cost, region.Days);
    }
}
