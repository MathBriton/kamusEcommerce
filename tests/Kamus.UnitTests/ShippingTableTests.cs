using Kamus.Orders.Domain;

namespace Kamus.UnitTests;

public sealed class ShippingTableTests
{
    [Theory]
    [InlineData("SP", "Sudeste", 19.90)]
    [InlineData("rs", "Sul", 24.90)]
    [InlineData("DF", "Centro-Oeste", 29.90)]
    [InlineData("BA", "Nordeste", 34.90)]
    [InlineData("AM", "Norte", 39.90)]
    public void Frete_por_regiao(string state, string region, decimal cost)
    {
        var quote = ShippingTable.Quote(state, 100m);

        quote!.Region.Should().Be(region);
        quote.Cost.Should().Be(cost);
    }

    [Fact]
    public void Frete_gratis_a_partir_do_limite()
    {
        ShippingTable.Quote("AM", ShippingTable.FreeShippingThreshold)!.Cost.Should().Be(0);
        ShippingTable.Quote("AM", ShippingTable.FreeShippingThreshold - 0.01m)!.Cost.Should().Be(39.90m);
    }

    [Fact]
    public void Todas_as_27_UFs()
    {
        ShippingTable.States.Should().HaveCount(27).And.OnlyHaveUniqueItems();
        ShippingTable.Quote("XX", 10m).Should().BeNull();
    }
}
