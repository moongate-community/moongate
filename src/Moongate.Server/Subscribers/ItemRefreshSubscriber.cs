using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Redraws an item on the clients that can see it after its fields change. Which packet to send is
/// decided by where the item's root sits — loose on a map, worn by a mobile, or inside a container —
/// the same three-way split ModernUO's Item.ProcessDelta makes.
/// </summary>
public sealed class ItemRefreshSubscriber : IEventSubscriberRegistration
{
    private readonly IItemService _items;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IContainerOpenerRegistry _openers;
    private readonly IWorldService _world;

    public ItemRefreshSubscriber(
        IItemService items,
        IPersistenceService persistenceService,
        IContainerOpenerRegistry openers,
        IWorldService world
    )
    {
        _items = items;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _openers = openers;
        _world = world;
    }

    public Task OnItemChanged(ItemChangedEvent message, CancellationToken cancellationToken)
    {
        if (_items.GetById(message.Item) is not { } item)
        {
            return Task.CompletedTask;
        }

        var root = _items.RootOf(item);

        if (item.ParentContainerId != Serial.Zero)
        {
            RefreshInContainer(item, root);
        }
        else if (item.EquippedMobileId != Serial.Zero)
        {
            RefreshOnMobile(item);
        }

        // Nothing for a ground item: VisibilitySubscriber owns those now, because drawing one is
        // inseparable from remembering that the client has it -- and from telling the client when it
        // stops being there.

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<ItemChangedEvent>(OnItemChanged);

    /// <summary>
    /// Whoever is looking inside: the mobile carrying the root container, plus everyone the registry
    /// believes has it open. Openers that have wandered out of range are dropped as they are found,
    /// which is why no close packet is needed.
    /// </summary>
    private void RefreshInContainer(ItemEntity item, ItemEntity root)
    {
        var packet = new AddItemToContainerPacket(
            item.Id,
            (ushort)item.ItemId,
            (ushort)item.Amount,
            item.ContainerPosition,
            item.ParentContainerId,
            item.Hue
        );

        var owner = root.EquippedMobileId;

        if (owner != Serial.Zero)
        {
            _world.SendToPlayer(owner, packet);
        }

        var (mapId, position) = RootLocation(root, owner);

        foreach (var opener in _openers.OpenersOf(item.ParentContainerId))
        {
            if (opener == owner)
            {
                continue;
            }

            if (_mobiles.GetById(opener) is not { } mobile ||
                mobile.MapId != mapId ||
                !mobile.Position.InRange(position, PlayerSession.MaxViewRange))
            {
                _openers.Closed(item.ParentContainerId, opener);

                continue;
            }

            _world.SendToPlayer(opener, packet);
        }
    }

    /// <summary>The item is worn: its layer redraws on the paperdoll for everyone watching the wearer.</summary>
    private void RefreshOnMobile(ItemEntity item)
    {
        if (item.EquippedLayer is not { } layer || _mobiles.GetById(item.EquippedMobileId) is not { } wearer)
        {
            return;
        }

        _world.SendToPlayersInRange(
            wearer.MapId,
            wearer.Position,
            PlayerSession.MaxViewRange,
            new WornItemPacket(item.Id, (ushort)item.ItemId, layer, wearer.Id, item.Hue)
        );
    }

    /// <summary>
    /// Where the root container is in the world: on its wearer when it is worn, otherwise its own
    /// spot on the map. This is what an opener's distance is measured against.
    /// </summary>
    private (int MapId, Point3D Position) RootLocation(ItemEntity root, Serial owner)
        => owner != Serial.Zero && _mobiles.GetById(owner) is { } wearer
               ? (wearer.MapId, wearer.Position)
               : (root.MapId, root.Position);
}
