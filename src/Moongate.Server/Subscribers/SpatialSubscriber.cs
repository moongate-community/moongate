using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Feeds the spatial index from world lifecycle events: indexes the whole world once it is ready
/// (WorldReadyEvent fires on the game-loop thread, so the bootstrap is loop-affine by construction),
/// then every player as they enter, removing them when their session dies. Handlers are public so
/// tests can drive them without a dispatching event bus.
/// </summary>
public sealed class SpatialSubscriber : IEventSubscriberRegistration
{
    private readonly ISpatialIndexService _spatial;
    private readonly IEntityStore<AccountEntity, Serial> _accounts;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IEntityStore<ItemEntity, Serial> _items;

    public SpatialSubscriber(ISpatialIndexService spatial, IPersistenceService persistence)
    {
        _spatial = spatial;
        _accounts = persistence.GetStore<AccountEntity, Serial>();
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
        _items = persistence.GetStore<ItemEntity, Serial>();
    }

    public void Subscribe(IEventBus eventBus)
    {
        eventBus.Subscribe<WorldReadyEvent>(OnWorldReady);
        eventBus.Subscribe<PlayerEnteredWorldEvent>(OnPlayerEnteredWorld);
        eventBus.Subscribe<SessionDestroyedEvent>(OnSessionDestroyed);
    }

    public Task OnWorldReady(WorldReadyEvent message, CancellationToken cancellationToken)
    {
        // Accounts' MobileIds tell characters from NPCs (same discriminator as CharacterQueryService):
        // an offline character must not ghost the world, online ones arrive via PlayerEnteredWorldEvent.
        var playerCharacters = new HashSet<Serial>();

        foreach (var account in _accounts.GetAll())
        {
            foreach (var mobileId in account.MobileIds)
            {
                playerCharacters.Add(mobileId);
            }
        }

        foreach (var mobile in _mobiles.GetAll())
        {
            if (!playerCharacters.Contains(mobile.Id))
            {
                _spatial.AddOrUpdate(mobile);
            }
        }

        foreach (var item in _items.GetAll())
        {
            if (item.ParentContainerId == Serial.Zero && item.EquippedMobileId == Serial.Zero)
            {
                _spatial.AddOrUpdate(item);
            }
        }

        return Task.CompletedTask;
    }

    public Task OnPlayerEnteredWorld(PlayerEnteredWorldEvent message, CancellationToken cancellationToken)
    {
        _spatial.AddOrUpdate(message.Mobile);

        return Task.CompletedTask;
    }

    public Task OnSessionDestroyed(SessionDestroyedEvent message, CancellationToken cancellationToken)
    {
        if (message.Session.Character is { } character)
        {
            _spatial.Remove(character.Id);
        }

        return Task.CompletedTask;
    }
}
