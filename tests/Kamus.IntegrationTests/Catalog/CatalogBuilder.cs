using Kamus.Catalog.Domain;
using Kamus.Catalog.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Inventory.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Catalog;

/// <summary>
/// Monta um catálogo isolado por teste: cada instância usa uma categoria raiz única,
/// então os testes não enxergam os dados uns dos outros.
/// </summary>
internal sealed class CatalogBuilder(KamusApiFactory factory)
{
    private readonly string _root = $"t{Guid.NewGuid():N}"[..12];
    private readonly Dictionary<string, Category> _categories = [];
    private readonly List<Product> _products = [];
    private readonly Dictionary<Guid, int> _stock = [];
    private DateTimeOffset _clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static readonly ColorInfo Azul = new("Azul", "#3b6ea8");
    public static readonly ColorInfo Preto = new("Preto", "#1f1f1f");
    public static readonly ColorInfo Bege = new("Bege", "#d9c7a7");

    public string Root => _root;

    public string Path(string relative) => relative.Length == 0 ? _root : $"{_root}/{relative}";

    public CatalogBuilder Category(string relativePath)
    {
        var segments = relativePath.Length == 0 ? [] : relativePath.Split('/');
        Category parent = GetOrAdd(_root, null);
        foreach (var segment in segments)
        {
            parent = GetOrAdd($"{parent.Path}/{segment}", parent);
        }

        return this;
    }

    public Product Product(
        string name,
        string category,
        decimal price,
        decimal? salePrice = null,
        ColorInfo[]? colors = null,
        string[]? sizes = null,
        int stock = 5,
        Collection? collection = null)
    {
        Category(category);
        _clock = _clock.AddMinutes(1);

        var product = new Product(name, $"{_root}-{Kamus.Shared.Text.Slug.From(name)}", $"Descrição de {name}", "Kamus", _categories[Path(category)].Id, collection?.Id, _clock);
        foreach (var color in colors ?? [Azul])
        {
            var sizeList = sizes ?? ["P", "M", "G"];
            for (var i = 0; i < sizeList.Length; i++)
            {
                var sku = product.AddSku($"{product.Slug}-{color.Name}-{sizeList[i]}".ToUpperInvariant(), color, sizeList[i], i, price, salePrice);
                _stock[sku.Id] = stock;
            }

            product.AddImage(color.Name, $"products/{product.Slug}/{color.Name}.svg", name);
        }

        _products.Add(product);
        return product;
    }

    public void SetStock(Sku sku, int quantity) => _stock[sku.Id] = quantity;

    public async Task SaveAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Categories.AddRange(_categories.Values);
        db.Products.AddRange(_products);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IInventoryService>().SetStockAsync(_stock);
    }

    private Category GetOrAdd(string path, Category? parent)
    {
        if (!_categories.TryGetValue(path, out var category))
        {
            category = new Category(path.Split('/')[^1], path.Split('/')[^1], parent, _categories.Count);
            _categories[path] = category;
        }

        return category;
    }
}
