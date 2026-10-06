namespace Kamus.Inventory.Domain;

/// <summary>Estoque físico de um SKU.</summary>
internal sealed class StockLevel
{
    private StockLevel()
    {
    }

    public StockLevel(Guid skuId, int quantity)
    {
        SkuId = skuId;
        SetQuantity(quantity);
    }

    public Guid SkuId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Token de concorrência otimista (coluna de sistema <c>xmin</c> do Postgres).</summary>
    public uint Version { get; private set; }

    public void SetQuantity(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        Quantity = quantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
