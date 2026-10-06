using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Shared.Modules;

/// <summary>
/// Ponto de entrada de um módulo do monólito. O host descobre os módulos,
/// registra os serviços de cada um e mapeia seus endpoints.
/// </summary>
public interface IModule
{
    string Name { get; }

    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
