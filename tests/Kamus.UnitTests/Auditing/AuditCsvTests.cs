using System.Text;
using Kamus.Audit.Api;
using Kamus.Audit.Application;
using Kamus.Shared.Auditing;

namespace Kamus.UnitTests.Auditing;

public sealed class AuditCsvTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "'=HYPERLINK(\"http://x\")")]
    [InlineData("+5511999999999", "'+5511999999999")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("\rcmd", "'\rcmd")]
    [InlineData("Camisa = linho", "Camisa = linho")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Neutraliza_injecao_de_formula(string? value, string expected) =>
        AuditCsv.Neutralize(value).Should().Be(expected);

    [Theory]
    [InlineData("Areia; P", "\"Areia; P\"")]
    [InlineData("Camisa \"Linho\"", "\"Camisa \"\"Linho\"\"\"")]
    [InlineData("linha\nquebrada", "\"linha\nquebrada\"")]
    [InlineData("=1+1;2", "\"'=1+1;2\"")]
    [InlineData("simples", "simples")]
    public void Celula_entre_aspas_quando_preciso(string value, string expected) =>
        AuditCsv.Cell(value).Should().Be(expected);

    [Fact]
    public void Arquivo_tem_bom_separador_ponto_e_virgula_e_cabecalho_em_portugues()
    {
        var entry = new AuditEntryDto(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 10, 8, 14, 30, 5, TimeSpan.Zero),
            "catalog",
            "Sku",
            Guid.CreateVersion7(),
            "updated",
            "Product",
            Guid.CreateVersion7(),
            "=Camisa de Linho",
            "Areia · P",
            [new AuditChange("Preço", "R$ 249,90", "R$ 199,90"), new AuditChange("Promocional", null, "R$ 179,90")],
            new AuditActorDto("Admin", Guid.CreateVersion7(), "Administrador Kamus", "admin@kamus.dev"),
            "0HN:00000001",
            "10.0.0.7");

        var bytes = AuditCsv.Write([entry]);

        bytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        var lines = Encoding.UTF8.GetString(bytes.AsSpan(3)).Split("\r\n");
        lines[0].Should().StartWith("Data e hora (UTC);Módulo;Ação;Item;Detalhe;Alterações;Usuário");
        var cells = lines[1].Split(';');
        cells[0].Should().Be("2026-10-08 14:30:05");
        cells[1].Should().Be("Catálogo");
        cells[2].Should().Be("Editou variação");
        cells[3].Should().Be("'=Camisa de Linho");
        cells[5].Should().Be("Preço: R$ 249,90 → R$ 199,90 | Promocional: — → R$ 179,90");
        cells[7].Should().Be("Admin");
        lines[2].Should().BeEmpty();
    }
}
