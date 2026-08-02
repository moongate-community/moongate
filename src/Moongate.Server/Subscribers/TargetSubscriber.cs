using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Drops a session's pending target request when it goes.
/// <para>
/// Wired in the same change as the service on purpose. <c>IGumpService.CloseAll</c> shipped
/// declared, implemented and documented as running on logout, with nothing calling it — a method
/// nobody calls fails no test, so the guard against that is doing it together, not remembering to.
/// </para>
/// </summary>
public sealed class TargetSubscriber : IEventSubscriberRegistration
{
    private readonly IPlayerTargetService _targets;

    public TargetSubscriber(IPlayerTargetService targets)
    {
        _targets = targets;
    }

    public Task OnSessionDestroyed(SessionDestroyedEvent @event, CancellationToken cancellationToken)
    {
        _targets.Forget(@event.Session);

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<SessionDestroyedEvent>(OnSessionDestroyed);
}
