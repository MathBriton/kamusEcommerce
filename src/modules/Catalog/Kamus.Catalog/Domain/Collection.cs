namespace Kamus.Catalog.Domain;

/// <summary>Agrupamento editorial de produtos ("Nova coleção", "Outlet").</summary>
internal sealed class Collection
{
    private Collection()
    {
    }

    public Collection(string name, string slug, string description)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Slug = slug;
        Description = description;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public string Description { get; private set; } = null!;
}
