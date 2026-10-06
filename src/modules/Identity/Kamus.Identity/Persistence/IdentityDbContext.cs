using Kamus.Identity.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Identity.Persistence;

/// <summary>
/// Tabelas do ASP.NET Core Identity no schema <c>identity</c>. Também guarda as chaves do
/// Data Protection, para que os cookies de login sobrevivam a reinícios e funcionem com várias instâncias.
/// </summary>
internal sealed class KamusIdentityDbContext(DbContextOptions<KamusIdentityDbContext> options)
    : IdentityDbContext<KamusUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public const string Schema = "identity";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<KamusUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.FullName).HasMaxLength(150);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
