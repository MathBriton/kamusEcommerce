using Kamus.Payments.Contracts;
using Kamus.Payments.FakePay;

namespace Kamus.UnitTests;

public sealed class PaymentRulesTests
{
    private const string Secret = "whsec_test";
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    [Theory]
    [InlineData(FakePayTestCards.Approved, true)]
    [InlineData(FakePayTestCards.Declined, true)]
    [InlineData(FakePayTestCards.Timeout, true)]
    [InlineData(FakePayTestCards.DuplicateWebhook, true)]
    [InlineData("4242 4242 4242 4242", true)]
    [InlineData("4242424242424241", false)]
    [InlineData("1234", false)]
    [InlineData("", false)]
    public void Validacao_de_cartao_por_luhn(string card, bool valid) => CardNumberRules.IsValid(card).Should().Be(valid);

    [Fact]
    public void Assinatura_valida_e_aceita()
    {
        const string body = """{"id":"evt_1"}""";
        var header = FakePaySignature.Sign(body, Secret, Now);

        FakePaySignature.Verify(body, header, Secret, Now.AddSeconds(30), Tolerance).Should().BeTrue();
    }

    [Fact]
    public void Corpo_alterado_invalida_a_assinatura()
    {
        var header = FakePaySignature.Sign("""{"amount":10}""", Secret, Now);

        FakePaySignature.Verify("""{"amount":1000}""", header, Secret, Now, Tolerance).Should().BeFalse();
    }

    [Fact]
    public void Assinatura_antiga_e_rejeitada_contra_replay()
    {
        const string body = """{"id":"evt_1"}""";
        var header = FakePaySignature.Sign(body, Secret, Now);

        FakePaySignature.Verify(body, header, Secret, Now.AddMinutes(6), Tolerance).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("t=abc,v1=abc")]
    public void Cabecalho_malformado_e_rejeitado(string? header) =>
        FakePaySignature.Verify("{}", header, Secret, Now, Tolerance).Should().BeFalse();
}
