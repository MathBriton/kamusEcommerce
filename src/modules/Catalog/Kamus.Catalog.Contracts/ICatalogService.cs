namespace Kamus.Catalog.Contracts;

/// <summary>Contrato público do módulo Catalog para outros módulos (Cart, Orders).</summary>
public interface ICatalogService
{
    /// <summary>Dados atuais dos SKUs informados. SKUs inexistentes ou inativos não aparecem no resultado.</summary>
    Task<IReadOnlyDictionary<Guid, SkuSnapshot>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default);
}

public sealed record SkuSnapshot(
    Guid SkuId,
    string Code,
    Guid ProductId,
    string ProductName,
    string ProductPath,
    string Color,
    string Size,
    decimal Price,
    decimal? SalePrice,
    string? ImageUrl)
{
    /// <summary>Preço efetivamente cobrado: o promocional, quando existe.</summary>
    public decimal UnitPrice => SalePrice ?? Price;
}
