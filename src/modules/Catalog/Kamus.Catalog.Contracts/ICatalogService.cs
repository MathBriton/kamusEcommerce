namespace Kamus.Catalog.Contracts;

/// <summary>Contrato público do módulo Catalog para outros módulos (Cart, Orders).</summary>
public interface ICatalogService
{
    /// <summary>
    /// Dados atuais dos SKUs vendáveis entre os informados. SKUs inexistentes, de produtos não
    /// publicados ou na lixeira (SKU ou produto excluído) não aparecem no resultado.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SkuSnapshot>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Descrição dos SKUs para rótulos (ex.: auditoria do estoque). Inclui rascunhos e itens na
    /// lixeira; só SKUs expurgados (apagados de vez) ficam de fora do resultado.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SkuDescription>> DescribeSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default);
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

/// <summary>Identificação de um SKU para exibição, independente de estar ativo.</summary>
public sealed record SkuDescription(Guid SkuId, Guid ProductId, string ProductName, string Color, string Size, string Code);

/// <summary>
/// SKUs apagados de vez (expurgo da lixeira). Publicado pelo Catalog depois de gravar; quem guarda
/// dados por SKU (ex.: Inventory) remove os seus.
/// </summary>
public sealed record SkusPurged(IReadOnlyList<Guid> SkuIds);
