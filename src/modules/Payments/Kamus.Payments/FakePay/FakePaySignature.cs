using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Kamus.Payments.FakePay;

/// <summary>
/// Assinatura dos webhooks no formato <c>t=&lt;unix&gt;,v1=&lt;hex&gt;</c>, onde v1 = HMAC-SHA256(segredo, "t.corpo").
/// O timestamp assinado impede o reaproveitamento (replay) de uma requisição antiga.
/// </summary>
internal static class FakePaySignature
{
    public const string Header = "FakePay-Signature";

    public static string Sign(string payload, string secret, DateTimeOffset timestamp)
    {
        var t = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"t={t},v1={Compute(t, payload, secret)}";
    }

    public static bool Verify(string payload, string? header, string secret, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrEmpty(header))
        {
            return false;
        }

        var parts = header.Split(',', StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1]);

        if (!parts.TryGetValue("t", out var t) || !parts.TryGetValue("v1", out var v1)
            || !long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var unix))
        {
            return false;
        }

        if ((now - DateTimeOffset.FromUnixTimeSeconds(unix)).Duration() > tolerance)
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Compute(t, payload, secret));
        var actual = Encoding.ASCII.GetBytes(v1);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string Compute(string timestamp, string payload, string secret)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexStringLower(mac);
    }
}
