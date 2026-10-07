using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Kamus.Shared.Text;

public static class DeterministicGuid
{
    /// <summary>
    /// Guid v7 reproduzível: timestamp real nos 48 bits iniciais (mantém a ordenação temporal) e o
    /// restante derivado de <paramref name="key"/>. Usado em dados de seed, para que execuções
    /// diferentes gerem os mesmos ids e as mesmas ordenações (testes visuais estáveis).
    /// </summary>
    public static Guid V7(DateTimeOffset timestamp, string key)
    {
        Span<byte> bytes = stackalloc byte[16];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        hash.AsSpan(0, 16).CopyTo(bytes);

        Span<byte> ms = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(ms, timestamp.ToUnixTimeMilliseconds());
        ms[2..].CopyTo(bytes); // 48 bits de timestamp

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // versão 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC 4122
        return new Guid(bytes, bigEndian: true);
    }
}
