using Microsoft.EntityFrameworkCore;

namespace Kamus.Shared.Infrastructure;

public static class ConcurrencyRetry
{
    /// <summary>
    /// Executa <paramref name="operation"/> e, se outro processo alterou as mesmas linhas
    /// (concorrência otimista), descarta o estado rastreado e tenta de novo com dados frescos.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(DbContext db, Func<Task<T>> operation, int maxAttempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                db.ChangeTracker.Clear();
                await Task.Delay(Random.Shared.Next(5, 25 * attempt));
            }
        }
    }
}
