using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Shared.Auditing;

public static class AuditingServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="ICurrentActor"/> (scoped). Já chamado por <c>AddSharedInfrastructure</c>;
    /// exposto para hosts de teste que montam o próprio container.
    /// </summary>
    public static IServiceCollection AddCurrentActor(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentActor, CurrentActor>();
        return services;
    }
}
