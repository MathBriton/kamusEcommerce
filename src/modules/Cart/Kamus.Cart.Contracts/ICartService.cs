namespace Kamus.Cart.Contracts;

/// <summary>Contrato público do módulo Cart, usado pelo checkout.</summary>
public interface ICartService
{
    Task<IReadOnlyList<CartLine>> GetCustomerCartAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task ClearCustomerCartAsync(Guid customerId, CancellationToken cancellationToken = default);
}

/// <param name="PriceWhenAdded">Preço unitário no momento em que o item entrou no carrinho.</param>
public sealed record CartLine(Guid SkuId, int Quantity, decimal PriceWhenAdded);
