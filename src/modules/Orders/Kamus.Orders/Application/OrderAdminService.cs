using FluentValidation;
using Kamus.Identity.Contracts;
using Kamus.Inventory.Contracts;
using Kamus.Orders.Contracts;
using Kamus.Orders.Domain;
using Kamus.Orders.Persistence;
using Kamus.Shared.Auditing;
using Kamus.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Orders.Application;

/// <summary>Operação de pedidos no backoffice e consultas de relatório.</summary>
internal sealed class OrderAdminService(
    OrdersDbContext db,
    IInventoryService inventory,
    ICustomerDirectory customers,
    ICurrentActor currentActor,
    TimeProvider clock) : IOrderReports
{
    private static readonly OrderStatus[] Sold = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    public async Task<(IReadOnlyList<AdminOrderRow> Items, int Total)> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(o => o.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var digits = new string([.. search.Where(char.IsAsciiDigit)]);
            var term = $"%{search.Trim()}%";
            query = long.TryParse(digits, out var number)
                ? query.Where(o => o.Number == number || EF.Functions.ILike(o.Address.RecipientName, term))
                : query.Where(o => EF.Functions.ILike(o.Address.RecipientName, term));
        }

        var total = await query.CountAsync(ct);
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ([.. orders.Select(o => new AdminOrderRow(
            o.Id, o.DisplayNumber, o.Status.ToString(), o.Total, o.Items.Sum(i => i.Quantity),
            o.Address.RecipientName, o.Address.City, o.Address.State, o.CreatedAt))], total);
    }

    public async Task<Result<AdminOrderDetail>> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? OrderQueries.NotFoundError() : await ToAdminDetailAsync(order, ct);
    }

    public Task<Result<AdminOrderDetail>> ShipAsync(Guid id, string trackingCode, CancellationToken ct) =>
        MutateAsync(id, o => o.Ship(clock.GetUtcNow(), currentActor.Actor, trackingCode.Trim().ToUpperInvariant()), ct);

    public Task<Result<AdminOrderDetail>> DeliverAsync(Guid id, CancellationToken ct) =>
        MutateAsync(id, o => o.Deliver(clock.GetUtcNow(), currentActor.Actor), ct);

    public async Task<Result<AdminOrderDetail>> CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        var result = await MutateAsync(id, o => o.Cancel($"Cancelado pela loja: {reason.Trim()}", clock.GetUtcNow(), currentActor.Actor), ct);
        if (result.IsSuccess)
        {
            await inventory.ReleaseReservationAsync(id, ct);
        }

        return result;
    }

    public async Task<IReadOnlyList<OrderFact>> GetOrderFactsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.CreatedAt < to)
            .Select(o => new { o.Id, o.CreatedAt, o.Status, o.Total, Units = o.Items.Sum(i => i.Quantity) })
            .ToListAsync(cancellationToken);

        return [.. orders.Select(o => new OrderFact(o.Id, o.CreatedAt, o.Status.ToString(), o.Total, o.Units))];
    }

    public async Task<IReadOnlyList<SkuSales>> GetTopSkusAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken = default)
    {
        var items = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.CreatedAt < to && Sold.Contains(o.Status))
            .SelectMany(o => o.Items)
            .GroupBy(i => i.SkuId)
            .Select(g => new
            {
                SkuId = g.Key,
                Units = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.UnitPrice * i.Quantity),
            })
            .OrderByDescending(x => x.Units).ThenByDescending(x => x.Revenue)
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Nome, cor e imagem vêm do snapshot do pedido mais recente daquele SKU.
        var ids = items.Select(i => i.SkuId).ToList();
        var snapshots = (await db.Orders.AsNoTracking()
                .Where(o => Sold.Contains(o.Status))
                .OrderByDescending(o => o.CreatedAt)
                .SelectMany(o => o.Items)
                .Where(i => ids.Contains(i.SkuId))
                .ToListAsync(cancellationToken))
            .DistinctBy(i => i.SkuId)
            .ToDictionary(i => i.SkuId);

        // O snapshot do pedido independe do catálogo: SKU excluído ou expurgado continua no ranking.
        return [.. items.Select(i =>
        {
            var s = snapshots[i.SkuId];
            return new SkuSales(i.SkuId, s.ProductName, s.ProductPath, s.Color, s.Size, s.ImageUrl, i.Units, i.Revenue);
        })];
    }

    private async Task<Result<AdminOrderDetail>> MutateAsync(Guid id, Func<Order, Result> change, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return OrderQueries.NotFoundError();
        }

        var result = change(order);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await db.SaveChangesAsync(ct);
        return await ToAdminDetailAsync(order, ct);
    }

    private async Task<AdminOrderDetail> ToAdminDetailAsync(Order order, CancellationToken ct)
    {
        var customer = await customers.FindAsync(order.CustomerId, ct);
        return new AdminOrderDetail(OrderQueries.ToDetail(order, includeActors: true), order.CustomerId, customer?.Email, customer?.FullName, order.PaymentId);
    }
}

internal sealed class ShipOrderRequestValidator : AbstractValidator<ShipOrderRequest>
{
    public ShipOrderRequestValidator() =>
        RuleFor(r => r.TrackingCode).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Use letras, números e hífen (ex.: BR123456789BR).");
}

internal sealed class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(200);
}
