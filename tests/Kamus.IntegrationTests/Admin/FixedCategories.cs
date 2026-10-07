using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Admin;

/// <summary>Cria categorias com caminho fixo (ex.: "masculino/camisetas"), se ainda não existirem.</summary>
internal sealed class FixedCategories(KamusApiFactory factory)
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public async Task EnsureAsync(string path)
    {
        await Lock.WaitAsync();
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Category? parent = null;
            foreach (var segment in path.Split('/'))
            {
                var current = parent is null ? segment : $"{parent.Path}/{segment}";
                var category = await db.Categories.FirstOrDefaultAsync(c => c.Path == current);
                if (category is null)
                {
                    category = new Category(segment, segment, parent, 0);
                    db.Categories.Add(category);
                    await db.SaveChangesAsync();
                }

                parent = category;
            }
        }
        finally
        {
            Lock.Release();
        }
    }
}
