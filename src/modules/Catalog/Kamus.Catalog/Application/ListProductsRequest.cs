using FluentValidation;

namespace Kamus.Catalog.Application;

/// <summary>Filtros da PLP. Todos opcionais; <c>size</c> e <c>color</c> aceitam múltiplos valores.</summary>
public sealed record ListProductsRequest(
    string? Category,
    string? Collection,
    string[]? Size,
    string[]? Color,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Sort,
    string? Cursor,
    int? Limit)
{
    public const int DefaultLimit = 24;
    public const int MaxLimit = 60;

    public ProductSort ParsedSort => ProductSorts.Parse(Sort);

    public int EffectiveLimit => Limit ?? DefaultLimit;
}

public enum ProductSort
{
    Newest,
    PriceAsc,
    PriceDesc,
    Name,
}

public static class ProductSorts
{
    private static readonly Dictionary<string, ProductSort> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        ["newest"] = ProductSort.Newest,
        ["price_asc"] = ProductSort.PriceAsc,
        ["price_desc"] = ProductSort.PriceDesc,
        ["name"] = ProductSort.Name,
    };

    public static bool IsValid(string? value) => value is null || Values.ContainsKey(value);

    public static ProductSort Parse(string? value) =>
        value is not null && Values.TryGetValue(value, out var sort) ? sort : ProductSort.Newest;
}

internal sealed class ListProductsRequestValidator : AbstractValidator<ListProductsRequest>
{
    public ListProductsRequestValidator()
    {
        RuleFor(r => r.Limit).InclusiveBetween(1, ListProductsRequest.MaxLimit);
        RuleFor(r => r.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(r => r.MaxPrice).GreaterThanOrEqualTo(0);
        RuleFor(r => r)
            .Must(r => r.MinPrice is null || r.MaxPrice is null || r.MinPrice <= r.MaxPrice)
            .WithName("maxPrice")
            .WithMessage("O preço máximo deve ser maior ou igual ao mínimo.");
        RuleFor(r => r.Sort)
            .Must(ProductSorts.IsValid)
            .WithMessage("Ordenação inválida. Use newest, price_asc, price_desc ou name.");
        RuleFor(r => r.Size).Must(s => s is null || s.Length <= 20);
        RuleFor(r => r.Color).Must(c => c is null || c.Length <= 20);
    }
}
