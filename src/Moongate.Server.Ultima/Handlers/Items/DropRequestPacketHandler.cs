using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     Drops the item the player holds (0x08) into a container their character carries, or into the container of a
///     carried item it was dropped on; anything else bounces the item back to where it was. The hand is always freed,
///     and 0x25 shows the item where it really is.
/// </summary>
/// <remarks>
///     The position is brought inside the container's gump bounds; a drop on the container's icon (-1, -1) takes a
///     random spot. A container never goes into itself or into anything inside it.
/// </remarks>
public sealed class DropRequestPacketHandler : IPacketHandler<DropRequestPacket>
{
    private const short OnIcon = -1;

    private readonly ILogger _logger = Log.ForContext<DropRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly ITileDataService _tiles;
    private readonly IContainerLayoutService _layouts;
    private readonly IPacketSendService _sender;

    public DropRequestPacketHandler(
        IItemService items,
        ITileDataService tiles,
        IContainerLayoutService layouts,
        IPacketSendService sender
    )
    {
        _items = items;
        _tiles = tiles;
        _layouts = layouts;
        _sender = sender;
    }

    public void Handle(GameSession session, DropRequestPacket packet)
    {
        var held = session.Get(ItemSessionKeys.Held);
        session.Set(ItemSessionKeys.Held, null);

        if (held is null || held.Item != packet.Item || !_items.TryGet(held.Item, out var item))
        {
            _logger.Debug("Session {SessionId} dropped {Item} without holding it", session.SessionId, packet.Item);

            return;
        }

        if (!TryPlace(session, item, packet))
        {
            _logger.Debug("{Item} dropped on {Destination} bounces back", item, packet.Destination);
        }

        _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
    }

    private bool TryPlace(GameSession session, ItemEntity item, DropRequestPacket packet)
    {
        if (!packet.Destination.IsItem ||
            !_items.TryGet(packet.Destination, out var target) ||
            _items.GetOwner(target) != session.CharacterId)
        {
            return false;
        }

        if (IsContainer(target))
        {
            return TryPut(item, target, AtPosition(target, packet.X, packet.Y));
        }

        // Dropped on a carried item: into that item's container, where that item lies.
        if (target.ContainerId is not { } parent || !_items.TryGet(parent, out var container))
        {
            return false;
        }

        return TryPut(item, container, target.GridLocation!.Value);
    }

    private bool TryPut(ItemEntity item, ItemEntity container, Point2D position)
    {
        if (Encloses(item, container))
        {
            return false;
        }

        _items.MoveToContainer(item, container.Id, position);

        return true;
    }

    // Whether the container is the item or lies somewhere inside it.
    private bool Encloses(ItemEntity item, ItemEntity container)
    {
        var visited = new HashSet<Serial>();
        var current = container;

        while (visited.Add(current.Id))
        {
            if (current.Id == item.Id)
            {
                return true;
            }

            if (current.ContainerId is not { } parent || !_items.TryGet(parent, out var next))
            {
                return false;
            }

            current = next;
        }

        return true;
    }

    private Point2D AtPosition(ItemEntity container, short x, short y)
    {
        if (x == OnIcon && y == OnIcon)
        {
            return _layouts.RandomGridPosition(container.ItemId);
        }

        // Bounds hold the first corner and exclude the second.
        var bounds = _layouts.GetLayout(container.ItemId).Bounds;

        return new(Clamp(x, bounds.Start.X, bounds.End.X), Clamp(y, bounds.Start.Y, bounds.End.Y));
    }

    private bool IsContainer(ItemEntity item)
    {
        return _tiles.TryGetItem(item.ItemId, out var tile) && (tile.Flags & TileFlagType.Container) != 0;
    }

    private static int Clamp(int value, int start, int end)
    {
        return end > start ? Math.Clamp(value, start, end - 1) : start;
    }
}
