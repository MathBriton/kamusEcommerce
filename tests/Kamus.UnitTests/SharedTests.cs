using Kamus.Shared.Infrastructure;
using Kamus.Shared.Pagination;
using Kamus.Shared.Text;

namespace Kamus.UnitTests;

public sealed class SharedTests
{
    [Theory]
    [InlineData("Calça Jeans Slim", "calca-jeans-slim")]
    [InlineData("  Boné  Aba-Reta!! ", "bone-aba-reta")]
    [InlineData("Acessórios", "acessorios")]
    public void Slug(string input, string expected) => Kamus.Shared.Text.Slug.From(input).Should().Be(expected);

    [Fact]
    public void Cursor_faz_ida_e_volta()
    {
        var encoded = Cursor.Encode(new Sample(42, "x"));

        Cursor.TryDecode<Sample>(encoded, out var decoded).Should().BeTrue();
        decoded.Should().Be(new Sample(42, "x"));
        Cursor.TryDecode<Sample>("%%%", out _).Should().BeFalse();
    }

    [Fact]
    public void Converte_url_do_postgres()
    {
        var cs = ConnectionStrings.NormalizePostgres("postgresql://kamus:p%40ss@db.internal:6543/kamus_prod");

        cs.Should().Contain("Host=db.internal").And.Contain("Port=6543").And.Contain("Database=kamus_prod")
            .And.Contain("Username=kamus").And.Contain("Password=p@ss");
        ConnectionStrings.NormalizePostgres("Host=localhost").Should().Be("Host=localhost");
    }

    [Theory]
    [InlineData("redis://red-123:6379", "red-123:6379")]
    [InlineData("redis://default:segredo@cache:6380", "cache:6380,password=segredo")]
    [InlineData("rediss://user:pw@cache:6380", "cache:6380,password=pw,user=user,ssl=true")]
    [InlineData("localhost:6379", "localhost:6379")]
    public void Converte_url_do_redis(string url, string expected) => ConnectionStrings.NormalizeRedis(url).Should().Be(expected);

    [Fact]
    public void Guid_deterministico_e_v7_e_reproduzivel()
    {
        var at = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

        var a = DeterministicGuid.V7(at, "product:calca-jeans-slim");
        var b = DeterministicGuid.V7(at, "product:calca-jeans-slim");
        var c = DeterministicGuid.V7(at, "product:camiseta-basica");

        a.Should().Be(b);
        a.Should().NotBe(c);
        a.Version.Should().Be(7);
        DeterministicGuid.V7(at.AddMilliseconds(1), "x").CompareTo(DeterministicGuid.V7(at, "x")).Should().BePositive();
    }

    private sealed record Sample(int Id, string Name);
}
