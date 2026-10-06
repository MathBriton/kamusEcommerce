using Kamus.Inventory.Contracts;
using Kamus.Inventory.Domain;
using Kamus.Inventory.Persistence;
using Kamus.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Inventory.Application;

internal sealed class InventoryService(InventoryDbContext db, TimeProvider clock) : IInventoryService
{
    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailabilityAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default)
    {
        var levels = await db.StockLevels
            .AsNoTracking()
            .Where(s => skuIds.Contains(s.SkuId))
            .ToDictionaryAsync(s => s.SkuId, s => s.Quantity - s.Reserved, cancellationToken);

        // SKU sem registro de estoque = indisponível
        return skuIds.Distinct().ToDictionary(id => id, id => Math.Max(0, levels.GetValueOrDefault(id)));
    }

    public async Task SetStockAsync(IReadOnlyDictionary<Guid, int> quantities, CancellationToken cancellationToken = default)
    {
        var ids = quantities.Keys.ToList();
        var existing = await db.StockLevels
            .Where(s => ids.Contains(s.SkuId))
            .ToDictionaryAsync(s => s.SkuId, cancellationToken);

        foreach (var (skuId, quantity) in quantities)
        {
            if (existing.TryGetValue(skuId, out var level))
            {
                level.SetQuantity(quantity);
            }
            else
            {
                db.StockLevels.Add(new StockLevel(skuId, quantity));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ReservationResult> ReserveAsync(Guid orderId, IReadOnlyCollection<ReservationLine> lines, TimeSpan timeToLive, CancellationToken cancellationToken = default) =>
        ConcurrencyRetry.ExecuteAsync(db, async () =>
        {
            var existing = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
            if (existing is not null)
            {
                return existing.Status == ReservationStatus.Active
                    ? ReservationResult.Reserved(existing.ExpiresAt)
                    : ReservationResult.Failed([]);
            }

            var requested = lines
                .GroupBy(l => l.SkuId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            var ids = requested.Keys.ToList();
            var levels = await db.StockLevels
                .Where(s => ids.Contains(s.SkuId))
                .ToDictionaryAsync(s => s.SkuId, cancellationToken);

            var shortages = requested
                .Select(r => new StockShortage(r.Key, r.Value, levels.TryGetValue(r.Key, out var l) ? Math.Max(0, l.Available) : 0))
                .Where(s => s.Available < s.Requested)
                .ToList();

            if (shortages.Count > 0)
            {
                return ReservationResult.Failed(shortages);
            }

            foreach (var (skuId, quantity) in requested)
            {
                levels[skuId].Reserve(quantity);
            }

            var reservation = new Reservation(orderId, requested.Select(r => (r.Key, r.Value)), clock.GetUtcNow(), timeToLive);
            db.Reservations.Add(reservation);

            // Uma única transação: se outro checkout mexeu nas mesmas linhas de estoque,
            // o xmin não confere, nada é gravado e a operação é refeita com dados novos.
            await db.SaveChangesAsync(cancellationToken);
            return ReservationResult.Reserved(reservation.ExpiresAt);
        });

    public Task<bool> CommitReservationAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        ConcurrencyRetry.ExecuteAsync(db, async () =>
        {
            var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
            if (reservation is null || reservation.Status is ReservationStatus.Released or ReservationStatus.Expired)
            {
                return false;
            }

            if (reservation.Status == ReservationStatus.Committed)
            {
                return true;
            }

            var levels = await LoadLevelsAsync(reservation, cancellationToken);
            foreach (var line in reservation.Lines)
            {
                levels[line.SkuId].CommitReserved(line.Quantity);
            }

            reservation.Close(ReservationStatus.Committed, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return true;
        });

    public Task ReleaseReservationAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        ConcurrencyRetry.ExecuteAsync(db, async () =>
        {
            var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
            if (reservation is not { Status: ReservationStatus.Active })
            {
                return false;
            }

            await CloseAndReleaseAsync(reservation, ReservationStatus.Released, cancellationToken);
            return true;
        });

    /// <summary>Expira as reservas vencidas até <paramref name="now"/>. Retorna os pedidos afetados.</summary>
    public async Task<IReadOnlyList<Guid>> ExpireDueReservationsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var due = await db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt)
            .Select(r => r.OrderId)
            .Take(100)
            .ToListAsync(cancellationToken);

        var expired = new List<Guid>();
        foreach (var orderId in due)
        {
            var changed = await ConcurrencyRetry.ExecuteAsync(db, async () =>
            {
                db.ChangeTracker.Clear();
                var reservation = await db.Reservations.FirstAsync(r => r.OrderId == orderId, cancellationToken);
                if (reservation.Status != ReservationStatus.Active)
                {
                    return false; // confirmada ou liberada nesse meio-tempo
                }

                await CloseAndReleaseAsync(reservation, ReservationStatus.Expired, cancellationToken);
                return true;
            });

            if (changed)
            {
                expired.Add(orderId);
            }
        }

        return expired;
    }

    private async Task CloseAndReleaseAsync(Reservation reservation, ReservationStatus status, CancellationToken ct)
    {
        var levels = await LoadLevelsAsync(reservation, ct);
        foreach (var line in reservation.Lines)
        {
            levels[line.SkuId].ReleaseReserved(line.Quantity);
        }

        reservation.Close(status, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    private Task<Dictionary<Guid, StockLevel>> LoadLevelsAsync(Reservation reservation, CancellationToken ct)
    {
        var ids = reservation.Lines.Select(l => l.SkuId).ToList();
        return db.StockLevels.Where(s => ids.Contains(s.SkuId)).ToDictionaryAsync(s => s.SkuId, ct);
    }
}
