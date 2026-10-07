using Microsoft.EntityFrameworkCore;

namespace Kamus.Shared.Infrastructure;

/// <summary>Aplica as migrations de um módulo. Executado na inicialização quando habilitado.</summary>
public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}

/// <summary>Popula dados na inicialização, após todas as migrations, em ordem crescente.</summary>
public interface IDataSeeder
{
    int Order { get; }

    /// <summary>
    /// Dados de exemplo (catálogo fictício) só rodam com <c>Seed:Enabled</c>. Dados essenciais
    /// (ex.: papel e usuário administrador) rodam sempre.
    /// </summary>
    bool IsSampleData => true;

    Task SeedAsync(CancellationToken cancellationToken);
}

internal sealed class DbContextMigrator<TContext>(TContext context) : IDatabaseMigrator
    where TContext : DbContext
{
    public Task MigrateAsync(CancellationToken cancellationToken) => context.Database.MigrateAsync(cancellationToken);
}
