using Kamus.Identity.Contracts;
using Kamus.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Identity;

internal sealed class CustomerDirectory(KamusIdentityDbContext db) : ICustomerDirectory
{
    public async Task<CustomerInfo?> FindAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == customerId)
            .Select(u => new CustomerInfo(u.Id, u.Email!, u.FullName))
            .FirstOrDefaultAsync(cancellationToken);
}
