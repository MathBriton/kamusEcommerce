namespace Kamus.Inventory.Domain;

/// <summary>
/// Estoque de um SKU. <see cref="Quantity"/> é o estoque físico; <see cref="Reserved"/> é a soma
/// das reservas ativas (pedidos aguardando pagamento). Disponível = físico − reservado.
/// </summary>
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

    public int Reserved { get; private set; }

    public int Available => Quantity - Reserved;

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Token de concorrência otimista (coluna de sistema <c>xmin</c> do Postgres).</summary>
    public uint Version { get; private set; }

    public void SetQuantity(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        Quantity = quantity;
        Touch();
    }

    public void Reserve(int quantity)
    {
        if (quantity > Available)
        {
            throw new InvalidOperationException($"Estoque insuficiente para o SKU {SkuId}.");
        }

        Reserved += quantity;
        Touch();
    }

    public void ReleaseReserved(int quantity)
    {
        Reserved = Math.Max(0, Reserved - quantity);
        Touch();
    }

    /// <summary>Converte reserva em baixa definitiva do estoque físico.</summary>
    public void CommitReserved(int quantity)
    {
        Reserved = Math.Max(0, Reserved - quantity);
        Quantity = Math.Max(0, Quantity - quantity);
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
