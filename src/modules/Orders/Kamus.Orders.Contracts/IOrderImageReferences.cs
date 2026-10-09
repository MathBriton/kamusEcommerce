namespace Kamus.Orders.Contracts;

/// <summary>
/// Imagens que os pedidos ainda exibem: o item do pedido guarda a URL da foto do produto no momento
/// da compra (snapshot). Quem apaga arquivos do catálogo pergunta aqui antes, para pedidos antigos
/// não perderem a miniatura.
/// </summary>
public interface IOrderImageReferences
{
    /// <summary>Das URLs informadas, as que algum item de pedido usa.</summary>
    Task<IReadOnlySet<string>> FindReferencedAsync(IReadOnlyCollection<string> imageUrls, CancellationToken cancellationToken = default);
}
