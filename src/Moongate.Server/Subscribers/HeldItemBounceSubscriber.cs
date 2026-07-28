using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Items;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Closes the drag-and-drop limbo: a player who disconnects while holding an item would otherwise
/// leave it attached to nothing. SessionDestroyedEvent is loop-affine, so this may touch world state
/// directly. The handler is public so tests can drive it without a dispatching event bus.
/// </summary>
public sealed class HeldItemBounceSubscriber : IEventSubscriberRegistration
{
    private readonly IDragDropService _dragDrop;

    public HeldItemBounceSubscriber(IDragDropService dragDrop)
    {
        _dragDrop = dragDrop;
    }

    public Task OnSessionDestroyed(SessionDestroyedEvent message, CancellationToken cancellationToken)
    {
        var session = message.Session;

        if (session.HeldItemId != Serial.Zero && session.Character is { } character)
        {
            _dragDrop.Bounce(character, session.HeldItemId, session.HeldItemOrigin);
            session.ClearHold();
        }

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<SessionDestroyedEvent>(OnSessionDestroyed);
}
