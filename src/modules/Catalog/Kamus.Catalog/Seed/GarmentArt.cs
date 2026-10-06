using System.Globalization;

namespace Kamus.Catalog.Seed;

internal enum Garment
{
    Tee,
    Shirt,
    Blouse,
    Pants,
    Shorts,
    Skirt,
    Dress,
    Jacket,
    Bag,
    Cap,
}

/// <summary>
/// Gera ilustrações SVG das peças, usadas no lugar de fotos no seed. Mantém o repositório leve,
/// evita questões de licença e dá uma identidade visual consistente à loja fictícia.
/// </summary>
internal static class GarmentArt
{
    private static readonly Dictionary<Garment, string> Paths = new()
    {
        [Garment.Tee] = "M190 170 L255 140 Q300 175 345 140 L410 170 L490 260 L430 305 L405 275 L405 640 L195 640 L195 275 L170 305 L110 260 Z",
        [Garment.Shirt] = "M190 170 L260 140 L300 190 L340 140 L410 170 L470 300 L470 560 L425 560 L410 330 L410 650 L190 650 L190 330 L175 560 L130 560 L130 300 Z",
        [Garment.Blouse] = "M205 175 L265 150 Q300 200 335 150 L395 175 L460 250 L415 300 L395 270 Q420 470 405 600 Q300 630 195 600 Q180 470 205 270 L185 300 L140 250 Z",
        [Garment.Pants] = "M200 130 L400 130 L435 690 L335 690 L300 330 L265 690 L165 690 Z",
        [Garment.Shorts] = "M190 220 L410 220 L445 500 L325 500 L300 360 L275 500 L155 500 Z",
        [Garment.Skirt] = "M225 190 L375 190 L460 620 Q300 650 140 620 Z",
        [Garment.Dress] = "M245 120 L275 120 Q300 160 325 120 L355 120 L370 260 L335 310 L465 690 Q300 720 135 690 L265 310 L230 260 Z",
        [Garment.Jacket] = "M185 165 L260 135 L300 220 L340 135 L415 165 L480 300 L480 600 L430 600 L415 330 L415 655 L185 655 L185 330 L170 600 L120 600 L120 300 Z",
        [Garment.Bag] = "M170 330 L430 330 L455 640 L145 640 Z M230 330 Q230 200 300 200 Q370 200 370 330 L345 330 Q345 230 300 230 Q255 230 255 330 Z",
        [Garment.Cap] = "M170 450 Q170 260 300 260 Q430 260 430 450 Z M150 450 L520 450 Q520 500 430 500 L150 500 Z",
    };

    private static readonly Dictionary<Garment, string> Details = new()
    {
        [Garment.Shirt] = "<path d=\"M300 190 L300 650\" stroke=\"rgba(0,0,0,.25)\" stroke-width=\"3\"/>" + Buttons(300, 230, 6, 70),
        [Garment.Jacket] = "<path d=\"M300 220 L300 655\" stroke=\"rgba(0,0,0,.3)\" stroke-width=\"4\"/><path d=\"M215 420 L265 420 M335 420 L385 420\" stroke=\"rgba(0,0,0,.25)\" stroke-width=\"4\"/>",
        [Garment.Pants] = "<path d=\"M200 175 L400 175\" stroke=\"rgba(0,0,0,.2)\" stroke-width=\"3\"/><path d=\"M300 175 L300 330\" stroke=\"rgba(0,0,0,.2)\" stroke-width=\"3\"/>",
        [Garment.Shorts] = "<path d=\"M190 260 L410 260\" stroke=\"rgba(0,0,0,.2)\" stroke-width=\"3\"/>",
        [Garment.Dress] = "<path d=\"M265 310 Q300 330 335 310\" stroke=\"rgba(0,0,0,.25)\" stroke-width=\"4\" fill=\"none\"/>",
        [Garment.Bag] = "<rect x=\"280\" y=\"380\" width=\"40\" height=\"22\" rx=\"4\" fill=\"rgba(0,0,0,.25)\"/>",
    };

    /// <summary>Renderiza a peça na cor informada. <c>variant</c>: 0 = peça inteira; 1 = detalhe ampliado.</summary>
    public static string Render(Garment garment, string hex, int variant)
    {
        var background = Mix(hex, "#f6f2ec", 0.88);
        var shadow = Mix(hex, "#000000", 0.35);
        var transform = variant == 0 ? string.Empty : " transform=\"translate(-300 -160) scale(2)\"";
        var details = Details.GetValueOrDefault(garment, string.Empty);

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 800" width="600" height="800">
              <rect width="600" height="800" fill="{background}"/>
              <ellipse cx="300" cy="730" rx="200" ry="18" fill="rgba(0,0,0,.06)"/>
              <g{transform}>
                <path d="{Paths[garment]}" fill="{hex}" stroke="{shadow}" stroke-width="3" stroke-linejoin="round" fill-rule="evenodd"/>
                {details}
              </g>
              <text x="300" y="775" text-anchor="middle" font-family="Georgia, serif" font-size="16" letter-spacing="6" fill="rgba(0,0,0,.35)">KAMUS</text>
            </svg>
            """;
    }

    private static string Buttons(int x, int y, int count, int gap) =>
        string.Concat(Enumerable.Range(0, count).Select(i => $"<circle cx=\"{x}\" cy=\"{y + (i * gap)}\" r=\"5\" fill=\"rgba(0,0,0,.3)\"/>"));

    /// <summary>Mistura duas cores hex; <paramref name="weight"/> é o peso da segunda.</summary>
    private static string Mix(string a, string b, double weight)
    {
        static (int R, int G, int B) Parse(string hex) => (
            int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber),
            int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber),
            int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber));

        var (r1, g1, b1) = Parse(a);
        var (r2, g2, b2) = Parse(b);
        int Blend(int x, int y) => (int)Math.Round((x * (1 - weight)) + (y * weight));
        return $"#{Blend(r1, r2):x2}{Blend(g1, g2):x2}{Blend(b1, b2):x2}";
    }
}
