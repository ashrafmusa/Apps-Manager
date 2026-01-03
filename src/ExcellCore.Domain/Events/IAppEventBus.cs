using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Events;

public interface IAppEventBus
{
    Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : class;
}

public interface IAppEventHandler<in TEvent>
{
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
