using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>Feeds player lifecycle and sector-change events into player-driven sector activity.</summary>
public sealed class SectorActivitySubscriber : IEventSubscriberRegistration
{
    private readonly ISectorActivityService _activity;
    private readonly HashSet<Serial> _players = [];

    public SectorActivitySubscriber(ISectorActivityService activity)
    {
        _activity = activity;
    }

    public Task OnPlayerEnteredWorld(PlayerEnteredWorldEvent message, CancellationToken cancellationToken)
    {
        _players.Add(message.Mobile.Id);
        _activity.TrackPlayer(message.Mobile);

        return Task.CompletedTask;
    }

    public Task OnMobileChangedSector(MobileChangedSectorEvent message, CancellationToken cancellationToken)
    {
        if (_players.Contains(message.Mobile))
        {
            _activity.MovePlayer(message.Mobile, message.ToMapId, message.ToSectorX, message.ToSectorY);
        }

        return Task.CompletedTask;
    }

    public Task OnSessionDestroyed(SessionDestroyedEvent message, CancellationToken cancellationToken)
    {
        if (message.Session.Character is { } character)
        {
            _players.Remove(character.Id);
            _activity.UntrackPlayer(character.Id);
        }

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
    {
        eventBus.Subscribe<PlayerEnteredWorldEvent>(OnPlayerEnteredWorld);
        eventBus.Subscribe<MobileChangedSectorEvent>(OnMobileChangedSector);
        eventBus.Subscribe<SessionDestroyedEvent>(OnSessionDestroyed);
    }
}
