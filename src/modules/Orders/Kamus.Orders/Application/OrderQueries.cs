using Kamus.Inventory.Contracts;
using Kamus.Orders.Domain;
using Kamus.Orders.Persistence;
using Kamus.Shared.Auditing;
using Kamus.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Orders.Application;

internal sealed class OrderQueries(OrdersDbContext db, IInventoryService inventory, ICurrentActor currentActor, TimeProvider clock)
{
    public async Task<IReadOnlyList<OrderSummaryDto>> ListAsync(Guid customerId, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return [.. orders.Select(o => new OrderSummaryDto(
            o.Id, o.DisplayNumber, o.Status.ToString(), o.Total, o.Items.Sum(i => i.Quantity), o.Items[0].ImageUrl, o.CreatedAt))];
    }

    public async Task<Result<OrderDetailDto>> GetAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        return order is null ? NotFound() : ToDetail(order);
    }

    public async Task<Result<OrderDetailDto>> CancelAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        if (order is null)
        {
            return NotFound();
        }

        var customer = currentActor.Customer();
        using var actingAsCustomer = currentActor.ActAs(customer);
        var result = order.Cancel("Cancelado pelo cliente.", clock.GetUtcNow(), customer);
        if (result.IsFailure)
        {
            return Error.Conflict("orders.cannot_cancel", "Este pedido não pode mais ser cancelado.");
        }

        await db.SaveChangesAsync(ct);
        await inventory.ReleaseReservationAsync(order.Id, ct);
        return ToDetail(order);
    }

    /// <summary>Simula a operação logística (sem painel administrativo no MVP).</summary>
    public async Task<Result<OrderDetailDto>> AdvanceFulfillmentAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        if (order is null)
        {
            return NotFound();
        }

        // Não é o cliente que despacha: a simulação faz o papel da operação logística.
        var actor = OrderActors.FulfillmentSimulation;
        using var actingAsLogistics = currentActor.ActAs(actor);
        var result = order.Status switch
        {
            OrderStatus.Paid => order.Ship(clock.GetUtcNow(), actor),
            OrderStatus.Shipped => order.Deliver(clock.GetUtcNow(), actor),
            _ => Error.Conflict("orders.invalid_transition", "Só pedidos pagos ou enviados avançam na entrega."),
        };

        if (result.IsFailure)
        {
            return result.Error!;
        }

        await db.SaveChangesAsync(ct);
        return ToDetail(order);
    }

    public static Error NotFoundError() => NotFound();

    private static Error NotFound() => Error.NotFound("orders.not_found", "Pedido não encontrado.");

    /// <summary>
    /// Detalhe do pedido. Quem fez cada mudança (<paramref name="includeActors"/>) só vai para o
    /// backoffice: o cliente não precisa ver o nome de quem opera a loja.
    /// </summary>
    internal static OrderDetailDto ToDetail(Order o, bool includeActors = false) => new(
        o.Id,
        o.DisplayNumber,
        o.Status.ToString(),
        [.. o.Items.Select(i => new OrderItemDto(i.SkuId, i.SkuCode, i.ProductName, i.ProductPath, i.Color, i.Size, i.ImageUrl, i.UnitPrice, i.ListPrice, i.Quantity, i.LineTotal))],
        new AddressDto(o.Address.RecipientName, o.Address.PostalCode, o.Address.Street, o.Address.Number, o.Address.Complement, o.Address.District, o.Address.City, o.Address.State),
        new ShippingQuoteDto(o.Address.State, o.Shipping.Region, o.Shipping.Cost, o.Shipping.EstimatedDays),
        o.Subtotal,
        o.Total,
        o.CreatedAt,
        [.. o.History.Select(h => includeActors
            ? new OrderStatusChangeDto(h.Status.ToString(), h.At, h.Note, h.ActorKind, h.ActorName)
            : new OrderStatusChangeDto(h.Status.ToString(), h.At, h.Note))],
        OrderStateMachine.CanTransition(o.Status, OrderStatus.Cancelled),
        o.TrackingCode);
}
