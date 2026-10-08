namespace Kamus.Catalog.Application;

/// <summary>Textos da lixeira exibidos no backoffice (nomes, detalhes e mensagens de restauração).</summary>
internal static class TrashText
{
    public static string Skus(int count) => count == 1 ? "1 SKU" : $"{count} SKUs";

    public static string Images(int count) => count == 1 ? "1 imagem" : $"{count} imagens";

    /// <summary>Linha de apoio do produto: "Kamus Studio · 2 SKUs · 1 imagem".</summary>
    public static string ProductDetail(string brand, int skus, int images) => $"{brand} · {Skus(skus)} · {Images(images)}";

    /// <summary>"Calça Chino · Verde-oliva · 42".</summary>
    public static string SkuName(string product, string color, string size) => $"{product} · {color} · {size}";

    /// <summary>"Viseira de Praia · foto 2" (posição da imagem na cor, a partir de 1).</summary>
    public static string ImageName(string product, int sortOrder) => $"{product} · foto {sortOrder + 1}";

    /// <summary>
    /// "Boné Trucker voltou como estava: publicado, com 2 SKUs e 1 imagem." ou
    /// "Pochete voltou como rascunho, com 3 SKUs e 2 imagens."
    /// </summary>
    public static string ProductRestored(string name, bool published, int skus, int images) => published
        ? $"{name} voltou como estava: publicado, {Contents(skus, images)}."
        : $"{name} voltou como rascunho, {Contents(skus, images)}.";

    public static string SkuRestored(string product, string color, string size) =>
        $"SKU {color} · {size} restaurado em {product}, com o estoque que tinha.";

    public static string ImageRestored(string product, string color) =>
        $"Imagem restaurada na galeria de {product} (cor {color}).";

    private static string Contents(int skus, int images) => (skus, images) switch
    {
        (0, 0) => "sem SKUs nem imagens",
        (_, 0) => $"com {Skus(skus)} e nenhuma imagem",
        (0, _) => $"com {Images(images)} e nenhum SKU",
        _ => $"com {Skus(skus)} e {Images(images)}",
    };
}
