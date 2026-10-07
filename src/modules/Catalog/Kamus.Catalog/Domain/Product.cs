namespace Kamus.Catalog.Domain;

/// <summary>Produto pai. O que se vende de fato é o <see cref="Sku"/> (cor + tamanho).</summary>
internal sealed class Product
{
    private readonly List<Sku> _skus = [];
    private readonly List<ProductImage> _images = [];

    private Product()
    {
    }

    public Product(string name, string slug, string description, string brand, Guid categoryId, Guid? collectionId, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        Name = name;
        Slug = slug;
        Description = description;
        Brand = brand;
        CategoryId = categoryId;
        CollectionId = collectionId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Slug global e único; a URL pública é <c>{caminho-da-categoria}/{slug}</c>.</summary>
    public string Slug { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public string Brand { get; private set; } = null!;

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public Guid? CollectionId { get; private set; }

    public Collection? Collection { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<Sku> Skus => _skus;

    public IReadOnlyList<ProductImage> Images => _images;

    public Sku AddSku(string code, ColorInfo color, string size, int sizeOrder, decimal price, decimal? salePrice)
    {
        Sku.EnsureValidPrices(price, salePrice);
        if (_skus.Any(s => s.Color == color.Name && s.Size == size))
        {
            throw new InvalidOperationException($"Já existe o SKU {color.Name}/{size} neste produto.");
        }

        var sku = new Sku(Id, code, color, size, sizeOrder, price, salePrice);
        _skus.Add(sku);
        UpdatedAt = DateTimeOffset.UtcNow;
        return sku;
    }

    public ProductImage AddImage(string color, string storageKey, string alt)
    {
        var image = new ProductImage(Id, color, storageKey, alt, _images.Count(i => i.Color == color));
        _images.Add(image);
        UpdatedAt = DateTimeOffset.UtcNow;
        return image;
    }

    public ProductImage? RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image is not null)
        {
            _images.Remove(image);
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        return image;
    }

    /// <summary>O slug não muda depois de criado: ele faz parte das URLs já indexadas.</summary>
    public void Update(string name, string description, string brand, Guid categoryId, Guid? collectionId, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        Brand = brand;
        CategoryId = categoryId;
        CollectionId = collectionId;
        UpdatedAt = now;
    }

    /// <summary>Só vai para a vitrine com pelo menos um SKU (sem SKU não há preço nem estoque).</summary>
    public bool Activate(DateTimeOffset now)
    {
        if (_skus.Count == 0)
        {
            return false;
        }

        IsActive = true;
        UpdatedAt = now;
        return true;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Produtos criados pelo backoffice começam inativos, até ganharem SKUs.</summary>
    public static Product CreateDraft(string name, string slug, string description, string brand, Guid categoryId, Guid? collectionId, DateTimeOffset now)
    {
        var product = new Product(name, slug, description, brand, categoryId, collectionId, now) { IsActive = false };
        return product;
    }
}

internal sealed record ColorInfo(string Name, string Hex);

/// <summary>Variação vendável: cor + tamanho, com preço e preço promocional ("de/por").</summary>
internal sealed class Sku
{
    private Sku()
    {
    }

    internal Sku(Guid productId, string code, ColorInfo color, string size, int sizeOrder, decimal price, decimal? salePrice)
    {
        Id = Guid.CreateVersion7();
        ProductId = productId;
        Code = code;
        Color = color.Name;
        ColorHex = color.Hex;
        Size = size;
        SizeOrder = sizeOrder;
        Price = price;
        SalePrice = salePrice;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Color { get; private set; } = null!;

    public string ColorHex { get; private set; } = null!;

    public string Size { get; private set; } = null!;

    /// <summary>Ordem de exibição do tamanho (PP &lt; P &lt; M ...), já que a ordem alfabética não serve.</summary>
    public int SizeOrder { get; private set; }

    public decimal Price { get; private set; }

    public decimal? SalePrice { get; private set; }

    public void UpdatePrices(decimal price, decimal? salePrice)
    {
        EnsureValidPrices(price, salePrice);
        Price = price;
        SalePrice = salePrice;
    }

    internal static void EnsureValidPrices(decimal price, decimal? salePrice)
    {
        if (price <= 0)
        {
            throw new ArgumentException("O preço precisa ser positivo.", nameof(price));
        }

        if (salePrice is not null && (salePrice <= 0 || salePrice >= price))
        {
            throw new ArgumentException("O preço promocional precisa ser positivo e menor que o preço cheio.", nameof(salePrice));
        }
    }
}

/// <summary>Imagem de produto, agrupada por cor para a galeria da PDP.</summary>
internal sealed class ProductImage
{
    private ProductImage()
    {
    }

    internal ProductImage(Guid productId, string color, string storageKey, string alt, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        ProductId = productId;
        Color = color;
        StorageKey = storageKey;
        Alt = alt;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string Color { get; private set; } = null!;

    public string StorageKey { get; private set; } = null!;

    public string Alt { get; private set; } = null!;

    public int SortOrder { get; private set; }
}
