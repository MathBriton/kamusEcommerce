using Microsoft.EntityFrameworkCore;

namespace Kamus.Shared.Infrastructure;

/// <summary>Aplica as migrations de um módulo. Executado na inicialização quando habilitado.</summary>
public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}

/// <summary>Popula dados de exemplo. Executado após todas as migrations, em ordem crescente.</summary>
public interface IDataSeeder
{
    int Order { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}

internal sealed class DbContextMigrator<TContext>(TContext context) : IDatabaseMigrator
    where TContext : DbContext
{
    public Task MigrateAsync(CancellationToken cancellationToken) => context.Database.MigrateAsync(cancellationToken);
}
