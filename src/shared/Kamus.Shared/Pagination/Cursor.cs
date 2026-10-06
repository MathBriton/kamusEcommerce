using System.Buffers.Text;
using System.Text.Json;

namespace Kamus.Shared.Pagination;

/// <summary>
/// Cursor opaco para paginação keyset: serializa a chave de ordenação do último item
/// da página em JSON + base64url. O cliente só repassa o valor.
/// </summary>
public static class Cursor
{
    public static string Encode<T>(T value) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));

    public static bool TryDecode<T>(string? cursor, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(Base64Url.DecodeFromChars(cursor));
            return value is not null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }
}

public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
