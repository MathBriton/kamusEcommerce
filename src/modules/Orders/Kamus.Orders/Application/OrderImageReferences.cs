using Kamus.Orders.Contracts;
using Kamus.Orders.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Orders.Application;

internal sealed class OrderImageReferences(OrdersDbContext db) : IOrderImageReferences
{
    public async Task<IReadOnlySet<string>> FindReferencedAsync(IReadOnlyCollection<string> imageUrls, CancellationToken cancellationToken = default)
    {
        if (imageUrls.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var urls = imageUrls.Distinct(StringComparer.Ordinal).ToList();
        var used = await db.Orders.AsNoTracking()
            .SelectMany(o => o.Items)
            .Where(i => i.ImageUrl != null && urls.Contains(i.ImageUrl))
            .Select(i => i.ImageUrl!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return used.ToHashSet(StringComparer.Ordinal);
    }
}
