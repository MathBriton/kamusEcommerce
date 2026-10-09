using Kamus.Shared.Auditing;

namespace Kamus.Catalog.Domain;

/// <summary>Produto pai. O que se vende de fato é o <see cref="Sku"/> (cor + tamanho).</summary>
/// <remarks>
/// Exclusão lógica (<see cref="ISoftDeletable"/>): <c>db.Remove(produto)</c> manda o produto para a
/// lixeira junto com os SKUs e imagens carregados (mesmo <see cref="DeletedAt"/>). As coleções
/// <see cref="Skus"/> e <see cref="Images"/> só trazem itens na lixeira quando a consulta usa
/// <c>IgnoreQueryFilters()</c>; as regras abaixo consideram apenas os ativos.
/// </remarks>
internal sealed class Product : ISoftDeletable
{
    private readonly List<Sku> _skus = [];
    private readonly List<ProductImage> _images = [];

    private Product()
    {
    }

    public Product(string name, string slug, string description, string brand, Guid categoryId, Guid? collectionId, DateTimeOffset createdAt, Guid? id = null)
    {
        Id = id ?? Guid.CreateVersion7(createdAt);
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

    /// <summary>SKUs fora da lixeira entre os carregados.</summary>
    public int ActiveSkuCount => _skus.Count(s => s.DeletedAt is null);

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public string? DeletedByName { get; private set; }

    /// <summary>
    /// Concorrência otimista (coluna <c>xmin</c>): excluir de vez, restaurar e editar ao mesmo tempo
    /// falham em vez de um sobrescrever o outro (ex.: expurgar um item que acabou de ser restaurado).
    /// </summary>
    public uint Version { get; private set; }

    public Sku AddSku(string code, ColorInfo color, string size, int sizeOrder, decimal price, decimal? salePrice, Guid? id = null)
    {
        Sku.EnsureValidPrices(price, salePrice);
        if (_skus.Any(s => s.DeletedAt is null && s.Color == color.Name && s.Size == size))
        {
            throw new InvalidOperationException($"Já existe o SKU {color.Name}/{size} neste produto.");
        }

        var sku = new Sku(Id, code, color, size, sizeOrder, price, salePrice, id);
        _skus.Add(sku);
        UpdatedAt = DateTimeOffset.UtcNow;
        return sku;
    }

    /// <summary>A imagem nova vai para o fim da galeria da cor (depois da última ativa).</summary>
    public ProductImage AddImage(string color, string storageKey, string alt)
    {
        var last = _images.Where(i => i.DeletedAt is null && i.Color == color).Select(i => i.SortOrder).DefaultIfEmpty(-1).Max();
        var image = new ProductImage(Id, color, storageKey, alt, last + 1);
        _images.Add(image);
        UpdatedAt = DateTimeOffset.UtcNow;
        return image;
    }

    /// <summary>
    /// Tira a imagem da galeria. Quem chama faz o <c>db.Remove(imagem)</c>: ela vai para a lixeira
    /// (soft delete), com o arquivo preservado até o expurgo.
    /// </summary>
    public ProductImage? RemoveImage(Guid imageId, DateTimeOffset now)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId && i.DeletedAt is null);
        if (image is not null)
        {
            _images.Remove(image);
            UpdatedAt = now;
        }

        return image;
    }

    /// <summary>
    /// Produto publicado precisa de ao menos um SKU (sem SKU não há preço nem estoque): a última
    /// variação ativa só pode ser excluída depois de despublicar.
    /// </summary>
    public bool IsLastSkuOnSale(Guid skuId) =>
        IsActive && ActiveSkuCount == 1 && _skus.Any(s => s.Id == skuId && s.DeletedAt is null);

    /// <summary>
    /// Tira o SKU da lista de variações. Quem chama faz o <c>db.Remove(sku)</c>: ele vai para a
    /// lixeira com o estoque que tem (o Inventory não é alterado). A última variação de um produto
    /// publicado não sai (veja <see cref="IsLastSkuOnSale"/>): retorna <see langword="null"/>.
    /// </summary>
    public Sku? RemoveSku(Guid skuId, DateTimeOffset now)
    {
        var sku = _skus.FirstOrDefault(s => s.Id == skuId && s.DeletedAt is null);
        if (sku is null || IsLastSkuOnSale(skuId))
        {
            return null;
        }

        _skus.Remove(sku);
        UpdatedAt = now;
        return sku;
    }

    /// <summary>Marca o produto como alterado agora (ex.: uma variação voltou da lixeira).</summary>
    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    /// <summary>Tira o produto da lixeira (os filhos são restaurados um a um por quem chama).</summary>
    public void Restore() => (DeletedAt, DeletedById, DeletedByName) = (null, null, null);

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
        if (ActiveSkuCount == 0)
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
internal sealed class Sku : ISoftDeletable
{
    private Sku()
    {
    }

    internal Sku(Guid productId, string code, ColorInfo color, string size, int sizeOrder, decimal price, decimal? salePrice, Guid? id = null)
    {
        Id = id ?? Guid.CreateVersion7();
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

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public string? DeletedByName { get; private set; }

    /// <summary>
    /// Concorrência otimista (coluna <c>xmin</c>): excluir de vez, restaurar e editar ao mesmo tempo
    /// falham em vez de um sobrescrever o outro (ex.: expurgar um item que acabou de ser restaurado).
    /// </summary>
    public uint Version { get; private set; }

    public void Restore() => (DeletedAt, DeletedById, DeletedByName) = (null, null, null);

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
internal sealed class ProductImage : ISoftDeletable
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

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public string? DeletedByName { get; private set; }

    /// <summary>
    /// Concorrência otimista (coluna <c>xmin</c>): excluir de vez, restaurar e editar ao mesmo tempo
    /// falham em vez de um sobrescrever o outro (ex.: expurgar um item que acabou de ser restaurado).
    /// </summary>
    public uint Version { get; private set; }

    public void Restore() => (DeletedAt, DeletedById, DeletedByName) = (null, null, null);

    /// <summary>Posição na galeria da cor (uma imagem restaurada vai para o fim, sem empatar com as atuais).</summary>
    public void MoveTo(int sortOrder) => SortOrder = sortOrder;
}
