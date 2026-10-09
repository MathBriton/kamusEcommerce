using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Shared.Infrastructure;

/// <summary>
/// Concorrência otimista que sobrou sem tratamento (ex.: duas pessoas editando o mesmo produto no
/// backoffice): responde 409 com uma mensagem útil em vez de 500. Casos de uso disputados continuam
/// usando <see cref="ConcurrencyRetry"/> para tentar de novo com dados frescos.
/// </summary>
public sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public const string Code = "concurrency_conflict";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = Code,
                Detail = "Outra pessoa alterou este item ao mesmo tempo. Recarregue a página e tente de novo.",
                Extensions = { ["code"] = Code },
            },
        });
    }
}
