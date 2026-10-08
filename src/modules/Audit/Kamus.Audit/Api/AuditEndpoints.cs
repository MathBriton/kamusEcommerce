using Kamus.Audit.Application;
using Kamus.Shared.Results;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamus.Audit.Api;

/// <summary>Consulta da auditoria no backoffice: <c>/api/admin/audit/*</c> (papel Admin).</summary>
internal static class AuditEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapAdminGroup("audit");

        group.MapGet("/entries", async (
            string? actor, string? module, string? action, DateOnly? from, DateOnly? to, int? page, int? pageSize,
            AuditQueries audit, CancellationToken ct) =>
            (await audit.ListAsync(new AuditFilter(actor, module, action, from, to), page, pageSize, ct)).ToHttp());

        group.MapGet("/entries.csv", async Task<IResult> (
            string? actor, string? module, string? action, DateOnly? from, DateOnly? to,
            AuditQueries audit, TimeProvider clock, CancellationToken ct) =>
        {
            var entries = await audit.ExportAsync(new AuditFilter(actor, module, action, from, to), ct);
            if (entries.IsFailure)
            {
                return entries.Error!.ToProblem();
            }

            var fileName = $"auditoria-{clock.GetUtcNow():yyyy-MM-dd}.csv";
            return TypedResults.File(AuditCsv.Write(entries.Value), "text/csv; charset=utf-8", fileName);
        });

        group.MapGet("/actors", async (AuditQueries audit, CancellationToken ct) =>
            TypedResults.Ok(await audit.ActorsAsync(ct)));

        group.MapGet("/subjects/{subjectType}/{subjectId:guid}", async (
            string subjectType, Guid subjectId, int? limit, AuditQueries audit, CancellationToken ct) =>
            TypedResults.Ok(await audit.SubjectAsync(subjectType, subjectId, limit, ct)));
    }
}
