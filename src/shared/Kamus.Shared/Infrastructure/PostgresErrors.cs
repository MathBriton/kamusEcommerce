using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kamus.Shared.Infrastructure;

public static class PostgresErrors
{
    /// <summary>Violação de unicidade (23505), usada como trava de idempotência.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
