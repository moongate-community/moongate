using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Turns world events into visibility updates. Every handler here runs on the game-loop thread, as
/// each of these events is loop-affine.
/// </summary>
public sealed class VisibilitySubscriber : IEventSubscriberRegistration
{
    private readonly IVisibilityService _visibility;
    private readonly ISessionManager _sessions;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IItemService _items;

    public VisibilitySubscriber(
        IVisibilityService visibility,
        ISessionManager sessions,
        IPersistenceService persistenceService,
        IItemService items
    )
    {
        _visibility = visibility;
        _sessions = sessions;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _items = items;
    }

    /// <summary>
    /// An item on the ground is drawn or undrawn by range like anything else. One that is no longer
    /// on the ground has left the world — picked up, or worn — and range says nothing about it, so
    /// everyone who had it drawn is told directly. Without that it would linger on their screen
    /// until the reconciliation sweep.
    /// </summary>
    public Task OnItemChanged(ItemChangedEvent @event, CancellationToken cancellationToken)
    {
        if (_items.GetById(@event.Item) is not { } item)
        {
            return Task.CompletedTask;
        }

        if (item.ParentContainerId != Serial.Zero || item.EquippedMobileId != Serial.Zero)
        {
            _visibility.Undraw(_sessions.All, item.Id);

            return Task.CompletedTask;
        }

        foreach (var session in _sessions.All)
        {
            if (session.Character is not null)
            {
                _visibility.UpdateFor(session, item);
            }
        }

        return Task.CompletedTask;
    }

    public Task OnMobileCreated(MobileCreatedEvent @event, CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.All)
        {
            if (session.Character is not null)
            {
                _visibility.UpdateFor(session, @event.Mobile);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A deleted mobile is gone from the world, so range says nothing about it: every client that
    /// knew it is told directly.
    /// </summary>
    public Task OnMobileDeleted(MobileDeletedEvent @event, CancellationToken cancellationToken)
    {
        _visibility.Undraw(_sessions.All, @event.Mobile.Id);

        return Task.CompletedTask;
    }

    public Task OnMobileMoved(MobileMovedEvent @event, CancellationToken cancellationToken)
    {
        // From the store, not from the sessions: an NPC has no session and would never be found.
        var mobile = _mobiles.GetById(@event.Mobile);

        if (mobile is null)
        {
            return Task.CompletedTask;
        }

        foreach (var session in _sessions.All)
        {
            if (session.Character is not { } character)
            {
                continue;
            }

            // The mover's own view translated entirely, so it is recomputed rather than patched.
            if (character.Id == @event.Mobile)
            {
                _visibility.Refresh(session);

                continue;
            }

            // Both positions matter: watching only the destination would never tell anyone that
            // something walked away from them.
            if (!Watches(character, session, @event.FromMapId, @event.FromPosition) &&
                !Watches(character, session, @event.ToMapId, @event.ToPosition))
            {
                continue;
            }

            _visibility.UpdateFor(session, mobile);
        }

        return Task.CompletedTask;
    }

    /// <summary>The login burst draws the player; this draws everything around them.</summary>
    public Task OnPlayerEnteredWorld(PlayerEnteredWorldEvent @event, CancellationToken cancellationToken)
    {
        if (_sessions.TryGet(@event.SessionId, out var session))
        {
            _visibility.Refresh(session);
        }

        return Task.CompletedTask;
    }

    public Task OnSessionDestroyed(SessionDestroyedEvent @event, CancellationToken cancellationToken)
    {
        _visibility.Forget(@event.Session);

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
    {
        eventBus.Subscribe<PlayerEnteredWorldEvent>(OnPlayerEnteredWorld);
        eventBus.Subscribe<MobileMovedEvent>(OnMobileMoved);
        eventBus.Subscribe<MobileCreatedEvent>(OnMobileCreated);
        eventBus.Subscribe<MobileDeletedEvent>(OnMobileDeleted);
        eventBus.Subscribe<ItemChangedEvent>(OnItemChanged);
        eventBus.Subscribe<SessionDestroyedEvent>(OnSessionDestroyed);
    }

    private static bool Watches(MobileEntity character, PlayerSession session, int mapId, Point3D position)
        => character.MapId == mapId && character.Position.InRange(position, session.ViewRange);
}
