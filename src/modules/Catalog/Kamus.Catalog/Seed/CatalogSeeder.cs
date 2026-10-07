using System.Text;
using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.Inventory.Contracts;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Storage;
using Kamus.Shared.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamus.Catalog.Seed;

/// <summary>
/// Popula o catálogo com cerca de 100 produtos fictícios, imagens e estoque. Determinístico
/// (semente fixa) e idempotente: só roda com o catálogo vazio.
/// </summary>
internal sealed class CatalogSeeder(
    CatalogDbContext db,
    IInventoryService inventory,
    IFileStorage storage,
    TimeProvider clock,
    ILogger<CatalogSeeder> logger) : IDataSeeder
{
    public int Order => 100;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(cancellationToken))
        {
            await RestoreMissingImagesAsync(cancellationToken);
            return;
        }

        var random = new Random(2026);
        var categories = CreateCategories();
        var collections = SeedCatalog.Collections
            .ToDictionary(c => c.Slug, c => new Collection(c.Name, c.Slug, c.Description));

        db.Categories.AddRange(categories.Values);
        db.Collections.AddRange(collections.Values);

        var now = clock.GetUtcNow();
        var products = new List<Product>();
        var stock = new Dictionary<Guid, int>();

        foreach (var template in SeedCatalog.Templates)
        {
            var category = categories[template.CategoryPath];
            foreach (var name in template.Names)
            {
                var createdAt = now.AddDays(-random.Next(0, 120)).AddMinutes(-random.Next(0, 1440));
                var onSale = random.NextDouble() < 0.3;
                var isNew = !onSale && now - createdAt < TimeSpan.FromDays(30);
                Collection? collection = onSale ? collections["outlet"]
                    : isNew ? collections["nova-colecao"]
                    : random.NextDouble() < 0.5 ? collections["essenciais"] : null;

                var colors = template.Palette.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList();
                var price = RoundPrice(template.MinPrice + (decimal)random.NextDouble() * (template.MaxPrice - template.MinPrice));
                decimal? salePrice = onSale ? RoundPrice(price * (decimal)(0.6 + (random.NextDouble() * 0.25))) : null;

                var slug = UniqueSlug(name, template.CategoryPath, products);
                var product = new Product(
                    name,
                    slug,
                    SeedCatalog.Describe(name, template.Material, colors[0].Name),
                    SeedCatalog.Brands[random.Next(SeedCatalog.Brands.Length)],
                    category.Id,
                    collection?.Id,
                    createdAt,
                    DeterministicGuid.V7(createdAt, $"product:{slug}"));

                foreach (var color in colors)
                {
                    foreach (var size in template.Sizes)
                    {
                        var code = $"KM{products.Count + 1:D3}-{Slug.From(color.Name).ToUpperInvariant()}-{size}";
                        var sku = product.AddSku(code, color, size, SeedCatalog.SizeOrder(size), price, salePrice, DeterministicGuid.V7(createdAt, $"sku:{code}"));
                        stock[sku.Id] = random.NextDouble() < 0.15 ? 0 : random.Next(1, 16);
                    }

                    for (var variant = 0; variant < 2; variant++)
                    {
                        var key = $"products/{product.Slug}/{Slug.From(color.Name)}-{variant + 1}.svg";
                        await SaveImageAsync(key, GarmentArt.Render(template.Garment, color.Hex, variant), cancellationToken);
                        product.AddImage(color.Name, key, variant == 0 ? $"{name} {color.Name.ToLowerInvariant()}" : $"{name} {color.Name.ToLowerInvariant()}, detalhe");
                    }
                }

                products.Add(product);
            }
        }

        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);

        await inventory.SetStockAsync(stock, cancellationToken);

        logger.LogInformation("Catálogo populado com {Products} produtos e {Skus} SKUs", products.Count, stock.Count);
    }

    /// <summary>
    /// Em hospedagens com disco efêmero (ex.: containers sem volume), os arquivos somem a cada deploy
    /// mas o banco continua populado. Regera as ilustrações que faltarem. (Na R4 as imagens vão para S3.)
    /// </summary>
    private async Task RestoreMissingImagesAsync(CancellationToken ct)
    {
        var templates = SeedCatalog.Templates.ToDictionary(t => t.CategoryPath);
        var images = await db.Products.AsNoTracking()
            .SelectMany(p => p.Images.Select(i => new
            {
                i.StorageKey,
                i.Color,
                CategoryPath = p.Category.Path,
                Hex = p.Skus.Where(s => s.Color == i.Color).Select(s => s.ColorHex).FirstOrDefault(),
            }))
            .ToListAsync(ct);

        var restored = 0;
        foreach (var image in images)
        {
            if (image.Hex is null || !templates.TryGetValue(image.CategoryPath, out var template)
                || await storage.ExistsAsync(image.StorageKey, ct))
            {
                continue;
            }

            var variant = image.StorageKey.EndsWith("-2.svg", StringComparison.Ordinal) ? 1 : 0;
            await SaveImageAsync(image.StorageKey, GarmentArt.Render(template.Garment, image.Hex, variant), ct);
            restored++;
        }

        if (restored > 0)
        {
            logger.LogInformation("{Count} imagens de produto regeradas", restored);
        }
    }

    private static Dictionary<string, Category> CreateCategories()
    {
        var result = new Dictionary<string, Category>();

        void Add(SeedCatalog.Node node, Category? parent, int order)
        {
            var category = new Category(node.Name, Slug.From(node.Name), parent, order);
            result[category.Path] = category;
            for (var i = 0; i < node.Children.Length; i++)
            {
                Add(node.Children[i], category, i);
            }
        }

        for (var i = 0; i < SeedCatalog.Categories.Length; i++)
        {
            Add(SeedCatalog.Categories[i], null, i);
        }

        return result;
    }

    private static string UniqueSlug(string name, string categoryPath, List<Product> existing)
    {
        var slug = Slug.From(name);
        if (existing.All(p => p.Slug != slug))
        {
            return slug;
        }

        return $"{slug}-{categoryPath.Split('/')[0]}";
    }

    /// <summary>Arredonda para preços "de vitrine": 129,90, 199,90...</summary>
    private static decimal RoundPrice(decimal value) => Math.Floor(value / 10) * 10 + 9.90m;

    private async Task SaveImageAsync(string key, string svg, CancellationToken ct)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        await storage.SaveAsync(key, content, "image/svg+xml", ct);
    }
}
