using Kamus.Shared.Infrastructure;

namespace Kamus.Api;

/// <summary>Aplica as migrations de todos os módulos e, em seguida, roda os seeders.</summary>
internal static class ModuleInitializer
{
    public static async Task InitializeModulesAsync(this WebApplication app)
    {
        var migrate = app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
        var seed = app.Configuration.GetValue<bool>("Seed:Enabled");

        await using var scope = app.Services.CreateAsyncScope();
        var ct = app.Lifetime.ApplicationStopping;

        if (migrate)
        {
            foreach (var migrator in scope.ServiceProvider.GetServices<IDatabaseMigrator>())
            {
                await migrator.MigrateAsync(ct);
            }
        }

        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>()
            .Where(s => seed || !s.IsSampleData)
            .OrderBy(s => s.Order);

        foreach (var seeder in seeders)
        {
            await seeder.SeedAsync(ct);
        }
    }
}
