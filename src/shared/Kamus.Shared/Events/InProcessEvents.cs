using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Shared.Events;

/// <summary>
/// Publica eventos entre módulos dentro do mesmo processo (MVP). Os tipos de evento vivem nos
/// <c>.Contracts</c> de quem publica; na R3 este barramento é trocado por outbox + broker.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}

public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}

internal sealed class InProcessEventPublisher(IServiceProvider services) : IEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        foreach (var handler in services.GetServices<IEventHandler<TEvent>>())
        {
            await handler.HandleAsync(@event, cancellationToken);
        }
    }
}

public static class EventServiceCollectionExtensions
{
    public static IServiceCollection AddInProcessEvents(this IServiceCollection services) =>
        services.AddScoped<IEventPublisher, InProcessEventPublisher>();

    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where THandler : class, IEventHandler<TEvent> =>
        services.AddScoped<IEventHandler<TEvent>, THandler>();
}
