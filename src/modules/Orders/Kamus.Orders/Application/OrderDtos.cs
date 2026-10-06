namespace Kamus.Orders.Application;

public sealed record AddressDto(
    string RecipientName,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State);

public sealed record PlaceOrderRequest(AddressDto Address, string CardNumber, decimal ExpectedTotal);

public sealed record PlacedOrder(Guid Id, string Number, string Status, decimal Total);

public sealed record CheckoutItem(
    Guid SkuId,
    string ProductName,
    string ProductPath,
    string Color,
    string Size,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal ListPrice,
    decimal LineTotal,
    int Available,
    string? Issue);

public sealed record CheckoutView(IReadOnlyList<CheckoutItem> Items, decimal Subtotal, decimal FreeShippingThreshold, bool CanPlaceOrder);

public sealed record ShippingQuoteDto(string State, string Region, decimal Cost, int EstimatedDays);

public sealed record OrderItemDto(
    Guid SkuId,
    string SkuCode,
    string ProductName,
    string ProductPath,
    string Color,
    string Size,
    string? ImageUrl,
    decimal UnitPrice,
    decimal ListPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderStatusChangeDto(string Status, DateTimeOffset At, string? Note);

public sealed record OrderSummaryDto(Guid Id, string Number, string Status, decimal Total, int ItemCount, string? ImageUrl, DateTimeOffset CreatedAt);

public sealed record OrderDetailDto(
    Guid Id,
    string Number,
    string Status,
    IReadOnlyList<OrderItemDto> Items,
    AddressDto Address,
    ShippingQuoteDto Shipping,
    decimal Subtotal,
    decimal Total,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderStatusChangeDto> History,
    bool CanCancel);
