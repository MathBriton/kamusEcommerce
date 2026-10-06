using Kamus.Catalog.Domain;

namespace Kamus.Catalog.Seed;

/// <summary>Dados fictícios da loja: árvore de categorias, paleta de cores e modelos de produto.</summary>
internal static class SeedCatalog
{
    public static readonly ColorInfo Preto = new("Preto", "#1f1f1f");
    public static readonly ColorInfo OffWhite = new("Off-white", "#f1ece2");
    public static readonly ColorInfo Marinho = new("Azul-marinho", "#1e2a4a");
    public static readonly ColorInfo Azul = new("Azul", "#3b6ea8");
    public static readonly ColorInfo JeansClaro = new("Jeans claro", "#8fb3d9");
    public static readonly ColorInfo JeansEscuro = new("Jeans escuro", "#2f4a6d");
    public static readonly ColorInfo Oliva = new("Verde-oliva", "#6b6f3a");
    public static readonly ColorInfo Terracota = new("Terracota", "#b4532a");
    public static readonly ColorInfo Caramelo = new("Caramelo", "#b9783f");
    public static readonly ColorInfo Bege = new("Bege", "#d9c7a7");
    public static readonly ColorInfo Vinho = new("Vinho", "#6d1f2f");
    public static readonly ColorInfo Rosa = new("Rosa", "#e7a1b0");
    public static readonly ColorInfo Amarelo = new("Amarelo", "#e3b23c");
    public static readonly ColorInfo Cinza = new("Cinza", "#8a8a8a");

    public static readonly string[] LetterSizes = ["PP", "P", "M", "G", "GG"];
    public static readonly string[] NumberSizes = ["36", "38", "40", "42", "44", "46"];
    public static readonly string[] OneSize = ["U"];

    /// <summary>Ordem global de exibição dos tamanhos: letras, depois números, depois tamanho único.</summary>
    public static int SizeOrder(string size) =>
        Array.IndexOf(LetterSizes, size) is var letter and >= 0 ? letter
        : Array.IndexOf(NumberSizes, size) is var number and >= 0 ? 10 + number
        : 20;

    public static readonly string[] Brands = ["Kamus", "Kamus Studio", "Kamus Praia"];

    public sealed record Node(string Name, Node[] Children)
    {
        public Node(string name)
            : this(name, [])
        {
        }
    }

    public static readonly Node[] Categories =
    [
        new("Feminino", [new("Vestidos"), new("Blusas"), new("Calças", [new("Jeans"), new("Alfaiataria")]), new("Saias"), new("Jaquetas")]),
        new("Masculino", [new("Camisetas"), new("Camisas"), new("Calças", [new("Jeans"), new("Sarja")]), new("Bermudas"), new("Jaquetas")]),
        new("Acessórios", [new("Bolsas"), new("Bonés")]),
    ];

    public sealed record Template(
        string CategoryPath,
        Garment Garment,
        string[] Sizes,
        decimal MinPrice,
        decimal MaxPrice,
        ColorInfo[] Palette,
        string Material,
        string[] Names);

    public static readonly Template[] Templates =
    [
        new("feminino/vestidos", Garment.Dress, LetterSizes, 189, 459, [Preto, Terracota, Rosa, Oliva, OffWhite, Vinho], "viscose",
            ["Vestido Midi Linho", "Vestido Longo Estampado", "Vestido Chemise", "Vestido Tubinho", "Vestido Envelope", "Vestido Ciganinha", "Vestido Slip", "Vestido Tricô", "Vestido Lese"]),
        new("feminino/blusas", Garment.Blouse, LetterSizes, 89, 229, [OffWhite, Rosa, Amarelo, Preto, Terracota, Azul], "algodão",
            ["Blusa Ombro a Ombro", "Blusa de Linho", "Regata Canelada", "Blusa Manga Bufante", "Body Decote V", "Blusa Cropped Tricô", "Bata Bordada", "Camisa Feminina de Seda"]),
        new("feminino/calcas/jeans", Garment.Pants, NumberSizes, 199, 349, [JeansClaro, JeansEscuro, Preto], "denim com elastano",
            ["Calça Jeans Wide Leg", "Calça Jeans Mom", "Calça Jeans Flare", "Calça Jeans Reta Cintura Alta", "Calça Jeans Skinny", "Calça Jeans Barrel"]),
        new("feminino/calcas/alfaiataria", Garment.Pants, NumberSizes, 229, 399, [Preto, Bege, Caramelo, OffWhite, Marinho], "crepe",
            ["Calça Pantalona", "Calça Alfaiataria Reta", "Calça Clochard", "Calça Pantacourt", "Calça Cenoura", "Calça Pijama de Linho"]),
        new("feminino/saias", Garment.Skirt, LetterSizes, 129, 269, [Preto, Caramelo, Terracota, Oliva, JeansClaro], "linho",
            ["Saia Midi Plissada", "Saia Jeans Curta", "Saia Longa Fluida", "Saia Lápis", "Saia Envelope", "Saia Evasê", "Saia Cargo"]),
        new("feminino/jaquetas", Garment.Jacket, LetterSizes, 289, 599, [Preto, Caramelo, JeansClaro, Oliva, Bege], "sarja",
            ["Jaqueta Jeans Oversized", "Blazer Alfaiataria", "Trench Coat", "Jaqueta Couro Sintético", "Casaqueto de Tricô", "Jaqueta Utilitária", "Colete de Alfaiataria"]),
        new("masculino/camisetas", Garment.Tee, LetterSizes, 69, 159, [Preto, OffWhite, Marinho, Oliva, Cinza, Terracota, Amarelo], "algodão",
            ["Camiseta Básica", "Camiseta Pima", "Camiseta Oversized", "Camiseta Henley", "Camiseta Listrada", "Camiseta Estampa Praia", "Camiseta Gola V", "Camiseta Pocket", "Polo Piquet"]),
        new("masculino/camisas", Garment.Shirt, LetterSizes, 159, 299, [OffWhite, Azul, Marinho, Oliva, Bege, Preto], "tricoline",
            ["Camisa Oxford", "Camisa de Linho", "Camisa Xadrez Flanela", "Camisa Jeans", "Camisa Manga Curta Viscose", "Camisa Social Slim", "Camisa Cubana", "Camisa Listrada Algodão"]),
        new("masculino/calcas/jeans", Garment.Pants, NumberSizes, 199, 329, [JeansClaro, JeansEscuro, Preto], "denim",
            ["Calça Jeans Slim", "Calça Jeans Reta", "Calça Jeans Skinny Destroyed", "Calça Jeans Carpinteiro", "Calça Jeans Relaxed", "Calça Jeans Black"]),
        new("masculino/calcas/sarja", Garment.Pants, NumberSizes, 179, 289, [Bege, Oliva, Marinho, Preto, Caramelo], "sarja",
            ["Calça Chino", "Calça Cargo", "Calça Jogger Sarja", "Calça Chino Slim", "Calça Pantalona Masculina", "Calça de Linho Masculina"]),
        new("masculino/bermudas", Garment.Shorts, NumberSizes, 109, 219, [Bege, Marinho, Oliva, JeansClaro, Preto, Terracota], "sarja",
            ["Bermuda Chino", "Bermuda Jeans", "Bermuda Cargo", "Short de Praia", "Bermuda de Linho", "Bermuda Moletom", "Bermuda Sarja Color"]),
        new("masculino/jaquetas", Garment.Jacket, LetterSizes, 259, 549, [Preto, JeansEscuro, Oliva, Caramelo, Marinho], "nylon",
            ["Jaqueta Bomber", "Jaqueta Jeans", "Jaqueta Corta-Vento", "Blazer de Linho", "Jaqueta Trucker Sarja", "Parka Leve", "Jaqueta Puffer"]),
        new("acessorios/bolsas", Garment.Bag, OneSize, 149, 389, [Caramelo, Preto, Bege, Terracota, Vinho], "couro sintético",
            ["Bolsa Tote", "Bolsa Baguete", "Mochila Urbana", "Bolsa Saco", "Pochete", "Bolsa Transversal", "Ecobag de Lona", "Clutch de Palha"]),
        new("acessorios/bones", Garment.Cap, OneSize, 79, 139, [Preto, Bege, Marinho, Oliva, OffWhite], "sarja",
            ["Boné Dad Hat", "Boné Trucker", "Bucket Hat", "Boné Aba Reta", "Viseira de Praia", "Boné Veludo"]),
    ];

    public static readonly (string Name, string Slug, string Description)[] Collections =
    [
        ("Nova coleção", "nova-colecao", "As peças que acabaram de chegar: linho, tons terrosos e cortes amplos."),
        ("Essenciais", "essenciais", "Básicos bem-feitos para montar qualquer look."),
        ("Outlet", "outlet", "Peças de coleções anteriores com preços especiais."),
    ];

    public static string Describe(string name, string material, string color) =>
        $"{name} em {material}, com acabamento cuidadoso e modelagem pensada para o dia a dia. "
        + $"Disponível em várias cores, como {color.ToLowerInvariant()}. Peça fictícia da coleção Kamus.";
}
