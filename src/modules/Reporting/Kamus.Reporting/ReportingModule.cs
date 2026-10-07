using Kamus.Shared.Modules;
using Kamus.Shared.Results;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Reporting;

public sealed class ReportingModule : IModule
{
    public string Name => "reporting";

    public void Register(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<ReportingService>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapAdminGroup("reports");

        group.MapGet("/overview", async (DateOnly? from, DateOnly? to, ReportingService reports, CancellationToken ct) =>
            (await reports.OverviewAsync(from, to, ct)).ToHttp());
    }
}
