using Kamus.Cart.Contracts;
using Kamus.Catalog.Contracts;
using Kamus.Inventory.Contracts;
using Kamus.Orders.Domain;
using Kamus.Orders.Persistence;
using Kamus.Payments.Contracts;
using Kamus.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Orders.Application;

internal sealed class CheckoutService(
    OrdersDbContext db,
    ICartService cart,
    ICatalogService catalog,
    IInventoryService inventory,
    IPaymentService payments,
    TimeProvider clock,
    IOptions<OrdersOptions> options,
    ILogger<CheckoutService> logger)
{
    /// <summary>Entrada no checkout: revalida preço e estoque de cada item do carrinho.</summary>
    public async Task<CheckoutView> GetAsync(Guid customerId, CancellationToken ct)
    {
        var items = await LoadItemsAsync(customerId, ct);
        var subtotal = items.Where(i => i.Issue is null or "price_changed").Sum(i => i.LineTotal);
        var canPlace = items.Count > 0 && items.All(i => i.Issue is null or "price_changed");
        return new CheckoutView(items, subtotal, ShippingTable.FreeShippingThreshold, canPlace);
    }

    public static Result<ShippingQuoteDto> QuoteShipping(string state, decimal subtotal) =>
        ShippingTable.Quote(state, subtotal) is { } quote
            ? new ShippingQuoteDto(state.ToUpperInvariant(), quote.Region, quote.Cost, quote.EstimatedDays)
            : Error.Validation("checkout.invalid_state", "UF inválida.");

    public async Task<Result<PlacedOrder>> PlaceOrderAsync(Guid customerId, PlaceOrderRequest request, CancellationToken ct)
    {
        var items = await LoadLinesAsync(customerId, ct);
        if (items.Count == 0)
        {
            return Error.Validation("checkout.empty_cart", "Seu carrinho está vazio.");
        }

        if (items.FirstOrDefault(i => i.Issue is not (null or "price_changed")) is { } blocked)
        {
            return Error.Conflict("checkout.item_unavailable", $"{blocked.ProductName} ({blocked.Size}) não está disponível na quantidade escolhida.");
        }

        var now = clock.GetUtcNow();
        var subtotal = items.Sum(i => i.LineTotal);
        var shipping = ShippingTable.Quote(request.Address.State, subtotal)!;
        var total = subtotal + shipping.Cost;
        if (total != request.ExpectedTotal)
        {
            // Preço mudou entre a tela de checkout e o clique em "pagar": o cliente precisa ver o valor novo.
            return Error.Conflict("checkout.total_changed", $"O total do pedido mudou para {total:C}. Revise e confirme novamente.");
        }

        var order = Order.Create(
            customerId,
            [.. items.Select(i => new OrderItem(i.SkuId, i.SkuCode, i.ProductName, i.ProductPath, i.Color, i.Size, i.ImageUrl, i.UnitPrice, i.ListPrice, i.Quantity))],
            ToAddress(request.Address),
            shipping,
            now);

        var reservation = await inventory.ReserveAsync(
            order.Id,
            [.. items.Select(i => new ReservationLine(i.SkuId, i.Quantity))],
            options.Value.PaymentTimeout,
            ct);

        if (!reservation.Succeeded)
        {
            var shortage = reservation.Shortages.Count > 0 ? reservation.Shortages[0] : null;
            var name = items.FirstOrDefault(i => i.SkuId == shortage?.SkuId)?.ProductName ?? "Um item";
            return Error.Conflict("checkout.insufficient_stock", shortage is { Available: > 0 }
                ? $"{name}: só restam {shortage.Available} unidade(s)."
                : $"{name} acabou de esgotar.");
        }

        try
        {
            order.StartPayment(now);
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            await payments.StartAsync(new StartPayment(order.Id, order.DisplayNumber, order.Total, request.CardNumber), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao criar o pedido {OrderId}; liberando a reserva", order.Id);
            await inventory.ReleaseReservationAsync(order.Id, CancellationToken.None);
            throw;
        }

        await cart.ClearCustomerCartAsync(customerId, ct);
        logger.LogInformation("Pedido {OrderNumber} criado, aguardando pagamento", order.DisplayNumber);

        return new PlacedOrder(order.Id, order.DisplayNumber, order.Status.ToString(), order.Total);
    }

    private sealed record Line(
        Guid SkuId,
        string SkuCode,
        string ProductName,
        string ProductPath,
        string Color,
        string Size,
        string? ImageUrl,
        int Quantity,
        decimal UnitPrice,
        decimal ListPrice,
        int Available,
        string? Issue)
    {
        public decimal LineTotal => UnitPrice * Quantity;
    }

    private async Task<List<CheckoutItem>> LoadItemsAsync(Guid customerId, CancellationToken ct) =>
        [.. (await LoadLinesAsync(customerId, ct)).Select(l => new CheckoutItem(
            l.SkuId, l.ProductName, l.ProductPath, l.Color, l.Size, l.ImageUrl, l.Quantity, l.UnitPrice, l.ListPrice, l.LineTotal, l.Available, l.Issue))];

    private async Task<List<Line>> LoadLinesAsync(Guid customerId, CancellationToken ct)
    {
        var cartLines = await cart.GetCustomerCartAsync(customerId, ct);
        if (cartLines.Count == 0)
        {
            return [];
        }

        var ids = cartLines.Select(l => l.SkuId).ToList();
        var skus = await catalog.GetSkusAsync(ids, ct);
        var availability = await inventory.GetAvailabilityAsync(ids, ct);

        return [.. cartLines.Select(l =>
        {
            var available = availability.GetValueOrDefault(l.SkuId);
            if (!skus.TryGetValue(l.SkuId, out var sku))
            {
                return new Line(l.SkuId, string.Empty, "Produto indisponível", string.Empty, string.Empty, string.Empty, null, l.Quantity, l.PriceWhenAdded, l.PriceWhenAdded, 0, "unavailable");
            }

            var issue = available == 0 ? "out_of_stock"
                : available < l.Quantity ? "insufficient_stock"
                : sku.UnitPrice != l.PriceWhenAdded ? "price_changed"
                : null;

            return new Line(sku.SkuId, sku.Code, sku.ProductName, sku.ProductPath, sku.Color, sku.Size, sku.ImageUrl, l.Quantity, sku.UnitPrice, sku.Price, available, issue);
        })];
    }

    private static ShippingAddress ToAddress(AddressDto a) => new(
        a.RecipientName.Trim(),
        new string([.. a.PostalCode.Where(char.IsAsciiDigit)]),
        a.Street.Trim(),
        a.Number.Trim(),
        string.IsNullOrWhiteSpace(a.Complement) ? null : a.Complement.Trim(),
        a.District.Trim(),
        a.City.Trim(),
        a.State.ToUpperInvariant());
}
