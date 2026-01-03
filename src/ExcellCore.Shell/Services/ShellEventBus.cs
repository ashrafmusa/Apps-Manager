using System;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Shell.Services;

public sealed class ShellEventBus : IAppEventBus
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ShellEventBus(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default) where TEvent : class
    {
        if (appEvent is null)
        {
            throw new ArgumentNullException(nameof(appEvent));
        }

        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IAppEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(appEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
