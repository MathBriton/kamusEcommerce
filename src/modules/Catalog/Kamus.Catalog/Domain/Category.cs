namespace Kamus.Catalog.Domain;

/// <summary>Categoria hierárquica (Masculino › Calças › Jeans). <see cref="Path"/> materializa a hierarquia.</summary>
internal sealed class Category
{
    private Category()
    {
    }

    public Category(string name, string slug, Category? parent, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Slug = slug;
        ParentId = parent?.Id;
        Path = parent is null ? slug : $"{parent.Path}/{slug}";
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    /// <summary>Caminho completo de slugs, ex.: <c>masculino/calcas/jeans</c>. Único.</summary>
    public string Path { get; private set; } = null!;

    public Guid? ParentId { get; private set; }

    public int SortOrder { get; private set; }
}
