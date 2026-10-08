using Kamus.Catalog.Application;
using Kamus.Catalog.Domain;

namespace Kamus.IntegrationTests.Catalog;

/// <summary>Regras da lixeira que não dependem do banco: invariantes do produto e textos exibidos.</summary>
public sealed class TrashRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly ColorInfo Preto = new("Preto", "#1f1f1f");

    private static Product Published(params string[] sizes)
    {
        var product = new Product("Boné Trucker", "bone-trucker", "Boné de tela.", "Kamus", Guid.CreateVersion7(), null, Now);
        for (var i = 0; i < sizes.Length; i++)
        {
            product.AddSku($"BT-PRETO-{sizes[i]}", Preto, sizes[i], i, 89.90m, null);
        }

        return product;
    }

    [Fact]
    public void Ultima_variacao_de_produto_publicado_nao_sai()
    {
        var product = Published("P", "M");
        var (p, m) = (product.Skus[0], product.Skus[1]);

        product.IsLastSkuOnSale(p.Id).Should().BeFalse();
        product.RemoveSku(p.Id, Now).Should().BeSameAs(p);

        product.IsLastSkuOnSale(m.Id).Should().BeTrue();
        product.RemoveSku(m.Id, Now).Should().BeNull();
        product.ActiveSkuCount.Should().Be(1);

        product.Deactivate(Now);
        product.IsLastSkuOnSale(m.Id).Should().BeFalse();
        product.RemoveSku(m.Id, Now).Should().BeSameAs(m);
        product.Activate(Now).Should().BeFalse("sem variação ativa não publica");
    }

    [Fact]
    public void Imagem_nova_vai_para_o_fim_da_galeria_da_cor()
    {
        var product = Published("U");
        var first = product.AddImage("Preto", "a.png", "a");
        product.AddImage("Preto", "b.png", "b");

        product.RemoveImage(first.Id, Now).Should().BeSameAs(first);
        product.RemoveImage(first.Id, Now).Should().BeNull("já saiu da galeria");
        var third = product.AddImage("Preto", "c.png", "c");

        third.SortOrder.Should().Be(2, "não pode empatar com a imagem que ficou");
        product.AddImage("Areia", "d.png", "d").SortOrder.Should().Be(0);
    }

    [Theory]
    [InlineData(true, 2, 1, "Boné Trucker voltou como estava: publicado, com 2 SKUs e 1 imagem.")]
    [InlineData(false, 3, 2, "Boné Trucker voltou como rascunho, com 3 SKUs e 2 imagens.")]
    [InlineData(false, 1, 0, "Boné Trucker voltou como rascunho, com 1 SKU e nenhuma imagem.")]
    [InlineData(false, 0, 1, "Boné Trucker voltou como rascunho, com 1 imagem e nenhum SKU.")]
    [InlineData(false, 0, 0, "Boné Trucker voltou como rascunho, sem SKUs nem imagens.")]
    public void Mensagem_de_restauracao_diz_como_o_produto_voltou(bool published, int skus, int images, string expected) =>
        TrashText.ProductRestored("Boné Trucker", published, skus, images).Should().Be(expected);

    [Fact]
    public void Nomes_e_detalhes_da_lixeira()
    {
        TrashText.ProductDetail("Kamus Studio", 1, 3).Should().Be("Kamus Studio · 1 SKU · 3 imagens");
        TrashText.SkuName("Calça Chino", "Verde-oliva", "42").Should().Be("Calça Chino · Verde-oliva · 42");
        TrashText.ImageName("Viseira de Praia", 1).Should().Be("Viseira de Praia · foto 2");
    }

    [Theory]
    [InlineData("product")]
    [InlineData("SKU")]
    [InlineData("Image")]
    public void Tipo_da_lixeira_aceita_maiusculas(string value)
    {
        TrashItemTypes.TryParse(value, out var type).Should().BeTrue();
        type.Code().Should().Be(value.ToLowerInvariant());
    }

    [Theory]
    [InlineData("all")]
    [InlineData("pedido")]
    [InlineData(null)]
    public void Tipo_desconhecido_nao_e_aceito(string? value) =>
        TrashItemTypes.TryParse(value, out _).Should().BeFalse();
}
