namespace Kamus.Shared.Auditing;

/// <summary>Ids dos registros de auditoria.</summary>
internal static class AuditIds
{
    private const int MaxSequence = (1 << 26) - 1;

    /// <summary>
    /// Guid v7 de <paramref name="occurredAt"/> em que os bits aleatórios iniciais (rand_a e o começo
    /// de rand_b) carregam <paramref name="sequence"/>. Registros do mesmo SaveChanges têm o mesmo
    /// instante; assim a ordenação por <c>(occurred_at, id)</c> segue a ordem em que foram gerados,
    /// em vez de depender de bits aleatórios (telas determinísticas).
    /// </summary>
    public static Guid Sequential(DateTimeOffset occurredAt, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, MaxSequence);

        Span<byte> bytes = stackalloc byte[16];
        Guid.CreateVersion7(occurredAt).TryWriteBytes(bytes, bigEndian: true, out _);

        bytes[6] = (byte)(0x70 | ((sequence >> 22) & 0x0F)); // versão 7 + 4 bits
        bytes[7] = (byte)(sequence >> 14);                   // 8 bits
        bytes[8] = (byte)(0x80 | ((sequence >> 8) & 0x3F)); // variante RFC 9562 + 6 bits
        bytes[9] = (byte)sequence;                           // 8 bits
        return new Guid(bytes, bigEndian: true);
    }
}
