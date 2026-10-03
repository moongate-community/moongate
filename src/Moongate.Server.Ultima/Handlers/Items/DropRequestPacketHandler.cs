using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Handlers.Items.Internal;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     Drops the item the player holds (0x08) into a container their character carries, onto a carried stack of the
///     same kind (they merge), into the container of a carried item it was dropped on, into a container lying on the
///     ground within reach or one inside it (onto a pile of the same kind there, they merge), on the ground within 2
///     tiles, or onto a ground stack of the same kind within reach; anything else bounces the item back to where it was. The hand
///     is always freed: 0x25 shows the item where it really is, and a ground item is shown to everyone in range.
/// </summary>
/// <remarks>
///     The position is brought inside the container's gump bounds; a drop on the container's icon (-1, -1) takes a
///     random spot. A container never goes into itself or into anything inside it.
/// </remarks>
public sealed class DropRequestPacketHandler : IPacketHandler<DropRequestPacket>
{
    public const string DropFunction = "on_drop";
    public const string CanDropFunction = "can_drop";
    public const string CanInsertFunction = "can_insert";

    private const short OnIcon = -1;
    private const int MaxStack = 60_000;

    private readonly ILogger _logger = Log.ForContext<DropRequestPacketHandler>();
    private const uint GroundDestination = uint.MaxValue;

    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly ITileDataService _tiles;
    private readonly IContainerLayoutService _layouts;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;
    private readonly IItemScriptService? _scripts;
    private readonly IBankService? _bank;

    public DropRequestPacketHandler(
        IItemService items,
        IMobileService mobiles,
        IWorldViewService view,
        ITileDataService tiles,
        IContainerLayoutService layouts,
        IPacketSendService sender,
        ITooltipService tooltips,
        IItemScriptService? scripts = null,
        IBankService? bank = null
    )
    {
        _bank = bank;
        _tooltips = tooltips;
        _scripts = scripts;
        _items = items;
        _mobiles = mobiles;
        _view = view;
        _tiles = tiles;
        _layouts = layouts;
        _sender = sender;
    }

    public void Handle(GameSession session, DropRequestPacket packet)
    {
        var held = session.Get(ItemSessionKeys.Held);
        session.Set(ItemSessionKeys.Held, null);

        if (held is not null && held.Item != packet.Item && _items.TryGet(held.Item, out var other))
        {
            // The hand is freed either way, so the held item must go back where it still is.
            _logger.Debug("Session {SessionId} named {Item} while holding {Held}", session.SessionId, packet.Item, other);
            HeldItemBounce.Return(session, other, _items, _mobiles, _view, _sender, _tooltips);

            return;
        }

        if (held is null || !_items.TryGet(held.Item, out var item))
        {
            _logger.Debug("Session {SessionId} dropped {Item} without holding it", session.SessionId, packet.Item);

            return;
        }

        var dropper = (long)session.CharacterId.Value;

        // The item's script may refuse to be put down anywhere.
        if (!Ask(session, held, item, CanDropFunction, dropper))
        {
            _logger.Debug("{Item} refuses to be dropped: it bounces back", item);
            HeldItemBounce.Return(session, item, _items, _mobiles, _view, _sender, _tooltips);

            return;
        }

        // The script of the container that would receive it may refuse it: asked once the rules allow the drop, and
        // once for the whole drop.
        bool? verdict = null;

        bool AllowsInto(ItemEntity container)
        {
            // A container its script removed while it was asked receives nothing.
            return verdict ??= Ask(session, held, container, CanInsertFunction, dropper, (long)item.Id.Value) &&
                               _items.TryGet(container.Id, out _);
        }

        // Taken off the paperdoll: players who came into range while it was held still see it worn.
        var wearer = item.MobileId is { } wearerId && _mobiles.TryGet(wearerId, out var worn) ? worn : null;

        if (TryMerge(session, item, packet.Destination, AllowsInto, out var stack))
        {
            TakenOff(wearer, item);
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(stack, session.UsesContainerGrid()));
            _sender.TrySend(session.SessionId, _tooltips.Info(stack));
            _sender.TrySend(session.SessionId, new RemoveEntityPacket(item.Id));
            Dropped(session, item);

            return;
        }

        var mobile = _mobiles.TryGet(session.CharacterId, out var live) ? live : null;

        if (mobile is not null && TryMergeOnGround(mobile, item, packet.Destination, out var groundStack))
        {
            // The character's leave saves the grown stack and deletes the absorbed item in one transaction.
            _items.Release(groundStack, session.CharacterId);
            _view.ItemAppeared(groundStack);
            TakenOff(wearer, item);
            _sender.TrySend(session.SessionId, new RemoveEntityPacket(item.Id));
            Dropped(session, item);

            return;
        }

        if (mobile is not null && TryMergeInChest(mobile, item, packet.Destination, AllowsInto, out var chestStack, out var holder))
        {
            // As on the ground: the character's leave saves the grown pile and deletes the absorbed item.
            _items.Release(chestStack, session.CharacterId);
            TakenOff(wearer, item);
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(chestStack, session.UsesContainerGrid()));
            _sender.TrySend(session.SessionId, _tooltips.Info(chestStack));
            _sender.TrySend(session.SessionId, new RemoveEntityPacket(item.Id));
            _view.ContainedItemAppeared(chestStack, holder, session.CharacterId);
            Dropped(session, item);

            return;
        }

        if (packet.Destination.Value == GroundDestination)
        {
            if (mobile is not null && _items.TryDropOnGround(mobile, item, packet.X, packet.Y))
            {
                // Its row still says the character carries it: the character's leave saves where it lies now.
                _items.Release(item, session.CharacterId);
                _view.ItemAppeared(item);
                Dropped(session, item);

                return;
            }
        }
        else if (mobile is not null && TryPlaceOnTheGround(mobile, item, packet, AllowsInto, out var chest))
        {
            // Its row still says the character carries it: the character's leave saves where it lies now.
            _items.Release(item, session.CharacterId);
            TakenOff(wearer, item);
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
            _sender.TrySend(session.SessionId, _tooltips.Info(item));
            _view.ContainedItemAppeared(item, chest, session.CharacterId);
            Dropped(session, item);

            return;
        }
        else if (TryPlace(session, item, packet, AllowsInto))
        {
            TakenOff(wearer, item);
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
            _sender.TrySend(session.SessionId, _tooltips.Info(item));
            Dropped(session, item);

            return;
        }

        _logger.Debug("{Item} dropped on {Destination} bounces back", item, packet.Destination);

        HeldItemBounce.Return(session, item, _items, _mobiles, _view, _sender, _tooltips);
    }

    // The item counts as held while a script is asked, so item.delete, item.consume and the like refuse it and the
    // drop goes on with an item that is still there.
    private bool Ask(GameSession session, HeldItem held, ItemEntity asked, string function, params object?[] args)
    {
        session.Set(ItemSessionKeys.Held, held);

        try
        {
            return _scripts.Allows(asked, function, args);
        }
        finally
        {
            session.Set(ItemSessionKeys.Held, null);
        }
    }

    // The item's script hears it was put down, after the packets: merged into a stack, the item is already gone.
    private void Dropped(GameSession session, ItemEntity item)
    {
        _scripts?.Queue(item, DropFunction, (long)session.CharacterId.Value);
    }

    private void TakenOff(MobileEntity? wearer, ItemEntity item)
    {
        if (wearer is not null)
        {
            _view.WornItemRemoved(wearer, item);
        }
    }

    // Onto a carried stack of the same kind: the stack grows and the held item is absorbed.
    private bool TryMerge(
        GameSession session,
        ItemEntity item,
        Serial destination,
        Func<ItemEntity, bool> allowsInto,
        out ItemEntity stack
    )
    {
        if (!_items.TryGet(destination, out stack!) ||
            stack.Id == item.Id ||
            stack.ContainerId is null ||
            _items.GetOwner(stack) != session.CharacterId ||
            !CanAccess(session, stack) ||
            !IsSameKind(stack, item) ||
            (long)stack.Amount + item.Amount > MaxStack ||
            !IsStackable(stack) ||
            (_items.TryGet(stack.ContainerId.Value, out var container) && !allowsInto(container)))
        {
            return false;
        }

        stack.Amount += item.Amount;
        _items.Absorb(item, session.CharacterId);

        return true;
    }

    // Onto a ground stack of the same kind within reach: the stack grows and the held item is absorbed.
    private bool TryMergeOnGround(MobileEntity mobile, ItemEntity item, Serial destination, out ItemEntity stack)
    {
        if (!_items.TryGet(destination, out stack!) ||
            stack.Id == item.Id ||
            stack.GroundLocation is null ||
            !_items.CanReach(mobile, stack) ||
            !IsSameKind(stack, item) ||
            (long)stack.Amount + item.Amount > MaxStack ||
            !IsStackable(stack))
        {
            return false;
        }

        stack.Amount += item.Amount;
        _items.Absorb(item, mobile.Id);

        return true;
    }

    // Onto a pile of the same kind inside a container lying on the ground within reach: the pile grows and the held
    // item is absorbed.
    private bool TryMergeInChest(
        MobileEntity mobile,
        ItemEntity item,
        Serial destination,
        Func<ItemEntity, bool> allowsInto,
        out ItemEntity stack,
        out ItemEntity chest
    )
    {
        chest = null!;

        if (!_items.TryGet(destination, out stack!) ||
            stack.Id == item.Id ||
            stack.ContainerId is null ||
            _items.GetGroundRoot(stack) is not { } root ||
            !_items.IsLyingOnGround(root) ||
            !_items.CanReach(mobile, root) ||
            !IsSameKind(stack, item) ||
            (long)stack.Amount + item.Amount > MaxStack ||
            !IsStackable(stack) ||
            (_items.TryGet(stack.ContainerId.Value, out var container) && !allowsInto(container)))
        {
            return false;
        }

        chest = root;
        stack.Amount += item.Amount;
        _items.Absorb(item, mobile.Id);

        return true;
    }

    // As ModernUO, two stacks merge only when nothing tells them apart: a rare stack must not absorb common coins.
    private static bool IsSameKind(ItemEntity stack, ItemEntity item)
    {
        return stack.ItemId == item.ItemId &&
               stack.Hue == item.Hue &&
               stack.TemplateId == item.TemplateId &&
               stack.Name == item.Name &&
               stack.Rarity == item.Rarity &&
               stack.Props is not { Count: > 0 } &&
               item.Props is not { Count: > 0 };
    }

    private bool TryPlace(GameSession session, ItemEntity item, DropRequestPacket packet, Func<ItemEntity, bool> allowsInto)
    {
        if (!packet.Destination.IsItem ||
            !_items.TryGet(packet.Destination, out var target) ||
            _items.GetOwner(target) != session.CharacterId ||
            !CanAccess(session, target))
        {
            return false;
        }

        if (IsContainer(target))
        {
            return TryPut(item, target, AtPosition(target, packet.X, packet.Y), packet.GridIndex, allowsInto);
        }

        // Dropped on a carried item: into that item's container, where that item lies.
        if (target.ContainerId is not { } parent || !_items.TryGet(parent, out var container))
        {
            return false;
        }

        return TryPut(item, container, target.GridLocation!.Value, packet.GridIndex, allowsInto);
    }

    // Into a container lying on the ground within reach, such as a treasure chest, or into one inside it; dropped on an
    // item inside, it goes beside that item.
    private bool TryPlaceOnTheGround(
        MobileEntity mobile,
        ItemEntity item,
        DropRequestPacket packet,
        Func<ItemEntity, bool> allowsInto,
        out ItemEntity chest
    )
    {
        chest = null!;

        if (!packet.Destination.IsItem ||
            !_items.TryGet(packet.Destination, out var target) ||
            _items.GetGroundRoot(target) is not { } root ||
            root.Id == item.Id ||
            !_items.IsLyingOnGround(root) ||
            !_items.CanReach(mobile, root))
        {
            return false;
        }

        chest = root;
        ItemEntity container;
        Point2D position;

        if (IsContainer(target))
        {
            container = target;
            position = AtPosition(target, packet.X, packet.Y);
        }
        else if (target.ContainerId is { } parent && _items.TryGet(parent, out var holder))
        {
            // Dropped on an item inside: beside it.
            container = holder;
            position = target.GridLocation!.Value;
        }
        else
        {
            // On a plain item lying on the ground: the drop falls through to the other rules.
            return false;
        }

        // Anyone may put things here: as a backpack on the client, it holds 125 at most.
        return _items.GetContents(container.Id).Count(other => other.Id != item.Id) < ContainerSlotUtils.SlotCount &&
               TryPut(item, container, position, packet.GridIndex, allowsInto);
    }

    // What lies in a bank box is reached only while the bank is open.
    private bool CanAccess(GameSession session, ItemEntity target)
    {
        return _bank is null || !_mobiles.TryGet(session.CharacterId, out var character) || _bank.CanAccess(session, character, target);
    }

    private bool TryPut(ItemEntity item, ItemEntity container, Point2D position, int gridIndex, Func<ItemEntity, bool> allowsInto)
    {
        if (Encloses(item, container) || !allowsInto(container))
        {
            return false;
        }

        // The slot the Enhanced Client asked for, or the next free one; the classic client sends 0.
        _items.MoveToContainer(item, container.Id, position, gridIndex);

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

    private bool IsStackable(ItemEntity item)
    {
        return _tiles.TryGetItem(item.ItemId, out var tile) && (tile.Flags & TileFlagType.Generic) != 0;
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
