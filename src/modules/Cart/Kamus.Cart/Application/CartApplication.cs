using Kamus.Cart.Contracts;
using Kamus.Catalog.Contracts;
using Kamus.Identity.Contracts;
using Kamus.Inventory.Contracts;
using Kamus.Shared.Events;
using Kamus.Shared.Results;
using Microsoft.Extensions.Options;

namespace Kamus.Cart.Application;

public sealed record CartItemView(
    Guid SkuId,
    string Code,
    string ProductName,
    string ProductPath,
    string Color,
    string Size,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal ListPrice,
    decimal PriceWhenAdded,
    decimal LineTotal,
    int Available,
    string? Issue);

/// <summary>Carrinho validado contra o catálogo e o estoque atuais.</summary>
public sealed record CartView(IReadOnlyList<CartItemView> Items, int ItemCount, decimal Subtotal, bool HasIssues);

public static class CartIssues
{
    /// <summary>O SKU saiu do catálogo.</summary>
    public const string Unavailable = "unavailable";

    public const string OutOfStock = "out_of_stock";

    /// <summary>Há estoque, mas menos do que a quantidade no carrinho.</summary>
    public const string InsufficientStock = "insufficient_stock";

    /// <summary>O preço mudou desde que o item foi adicionado (informativo).</summary>
    public const string PriceChanged = "price_changed";
}

internal sealed class CartApplication(
    CartStore store,
    CartOwnerResolver owners,
    ICatalogService catalog,
    IInventoryService inventory,
    IOptions<CartOptions> options) : ICartService, IEventHandler<CustomerSignedIn>
{
    private readonly CartOptions _options = options.Value;

    public async Task<CartView> GetCurrentAsync(CancellationToken ct)
    {
        var owner = owners.Current();
        return owner is null ? Empty : await BuildViewAsync(await store.GetAsync(owner.Value), ct);
    }

    public async Task<Result<CartView>> AddAsync(Guid skuId, int quantity, CancellationToken ct)
    {
        var sku = (await catalog.GetSkusAsync([skuId], ct)).GetValueOrDefault(skuId);
        if (sku is null)
        {
            return Error.NotFound("cart.sku_not_found", "Produto não encontrado.");
        }

        var owner = owners.CurrentOrCreate();
        var existing = await store.GetLineAsync(owner, skuId);
        if (existing is null && await store.CountLinesAsync(owner) >= _options.MaxItems)
        {
            return Error.Validation("cart.too_many_items", $"O carrinho aceita no máximo {_options.MaxItems} itens diferentes.");
        }

        var desired = (existing?.Quantity ?? 0) + quantity;
        var check = await CheckQuantityAsync(skuId, desired, ct);
        if (check.IsFailure)
        {
            return check.Error!;
        }

        await store.UpdateLineAsync(owner, skuId, current => new CartLine(
            skuId,
            Math.Min(_options.MaxQuantityPerItem, (current?.Quantity ?? 0) + quantity),
            sku.UnitPrice));

        return await BuildViewAsync(await store.GetAsync(owner), ct);
    }

    public async Task<Result<CartView>> SetQuantityAsync(Guid skuId, int quantity, CancellationToken ct)
    {
        var owner = owners.Current();
        if (owner is null || await store.GetLineAsync(owner.Value, skuId) is null)
        {
            return Error.NotFound("cart.item_not_found", "Item não está no carrinho.");
        }

        var check = await CheckQuantityAsync(skuId, quantity, ct);
        if (check.IsFailure)
        {
            return check.Error!;
        }

        await store.UpdateLineAsync(owner.Value, skuId, current => current is null ? null : current with { Quantity = quantity });
        return await BuildViewAsync(await store.GetAsync(owner.Value), ct);
    }

    public async Task<CartView> RemoveAsync(Guid skuId, CancellationToken ct)
    {
        var owner = owners.Current();
        if (owner is null)
        {
            return Empty;
        }

        await store.UpdateLineAsync(owner.Value, skuId, _ => null);
        return await BuildViewAsync(await store.GetAsync(owner.Value), ct);
    }

    public async Task<int> CountAsync()
    {
        var owner = owners.Current();
        if (owner is null)
        {
            return 0;
        }

        return (await store.GetAsync(owner.Value)).Sum(l => l.Quantity);
    }

    public Task<IReadOnlyList<CartLine>> GetCustomerCartAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        store.GetAsync(CartOwner.Customer(customerId));

    public Task ClearCustomerCartAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        store.ClearAsync(CartOwner.Customer(customerId));

    /// <summary>No login, o carrinho montado como visitante passa para a conta do cliente.</summary>
    public async Task HandleAsync(CustomerSignedIn @event, CancellationToken cancellationToken)
    {
        if (owners.VisitorId() is { } visitorId)
        {
            await store.MergeAsync(CartOwner.Visitor(visitorId), CartOwner.Customer(@event.CustomerId));
        }
    }

    private async Task<Result> CheckQuantityAsync(Guid skuId, int quantity, CancellationToken ct)
    {
        if (quantity > _options.MaxQuantityPerItem)
        {
            return Error.Validation("cart.max_quantity", $"Limite de {_options.MaxQuantityPerItem} unidades por item.");
        }

        var available = (await inventory.GetAvailabilityAsync([skuId], ct)).GetValueOrDefault(skuId);
        return quantity <= available
            ? Result.Success()
            : Error.Conflict("cart.insufficient_stock", available == 0
                ? "Este tamanho esgotou."
                : $"Só temos {available} unidade(s) disponível(is) deste item.");
    }

    private async Task<CartView> BuildViewAsync(IReadOnlyList<CartLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            return Empty;
        }

        var ids = lines.Select(l => l.SkuId).ToList();
        var skus = await catalog.GetSkusAsync(ids, ct);
        var availability = await inventory.GetAvailabilityAsync(ids, ct);

        var items = lines.Select(line =>
        {
            var available = availability.GetValueOrDefault(line.SkuId);
            if (!skus.TryGetValue(line.SkuId, out var sku))
            {
                return new CartItemView(line.SkuId, string.Empty, "Produto indisponível", string.Empty, string.Empty, string.Empty, null,
                    line.Quantity, line.PriceWhenAdded, line.PriceWhenAdded, line.PriceWhenAdded, 0, 0, CartIssues.Unavailable);
            }

            var issue = available == 0 ? CartIssues.OutOfStock
                : available < line.Quantity ? CartIssues.InsufficientStock
                : sku.UnitPrice != line.PriceWhenAdded ? CartIssues.PriceChanged
                : null;

            return new CartItemView(sku.SkuId, sku.Code, sku.ProductName, sku.ProductPath, sku.Color, sku.Size, sku.ImageUrl,
                line.Quantity, sku.UnitPrice, sku.Price, line.PriceWhenAdded, sku.UnitPrice * line.Quantity, available, issue);
        }).ToList();

        var purchasable = items.Where(i => i.Issue is null or CartIssues.PriceChanged).ToList();
        return new CartView(
            items,
            items.Sum(i => i.Quantity),
            purchasable.Sum(i => i.LineTotal),
            items.Any(i => i.Issue is CartIssues.Unavailable or CartIssues.OutOfStock or CartIssues.InsufficientStock));
    }

    private static readonly CartView Empty = new([], 0, 0, false);
}
