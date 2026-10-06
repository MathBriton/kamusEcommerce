using Npgsql;

namespace Kamus.Shared.Infrastructure;

/// <summary>
/// Plataformas gerenciadas (Render, Fly, Railway...) entregam conexões como URL
/// (<c>postgresql://user:pass@host:5432/db</c>, <c>redis://:pass@host:6379</c>). Npgsql e
/// StackExchange.Redis esperam outro formato, então convertemos quando necessário.
/// </summary>
public static class ConnectionStrings
{
    public static string NormalizePostgres(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            GssEncryptionMode = GssEncryptionMode.Disable,
        };

        if (uri.Query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }

    public static string NormalizeRedis(string value)
    {
        var secure = value.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase);
        if (!secure && !value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var parts = new List<string> { $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 6379)}" };
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length == 2 && userInfo[1].Length > 0)
        {
            parts.Add($"password={Uri.UnescapeDataString(userInfo[1])}");
            if (userInfo[0].Length > 0 && userInfo[0] != "default")
            {
                parts.Add($"user={Uri.UnescapeDataString(userInfo[0])}");
            }
        }

        if (secure)
        {
            parts.Add("ssl=true");
        }

        return string.Join(',', parts);
    }
}
