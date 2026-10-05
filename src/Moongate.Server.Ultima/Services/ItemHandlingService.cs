using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;
using Moongate.Server.Ultima.Interfaces.Items;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Makes, gives, consumes and deletes live items and tells the clients: what the <c>item</c> Lua module and the
///     services that hand out or take items share.
/// </summary>
public sealed class ItemHandlingService : IItemHandlingService
{
    private readonly IItemService _items;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly IWorldViewService _view;
    private readonly ITooltipService _tooltips;
    private readonly IItemFactoryService? _factory;
    private readonly IItemSerialPool? _serials;
    private readonly IContainerLayoutService? _layouts;
    private readonly IInventoryMutationGuard? _inventory;

    public ItemHandlingService(
        IItemService items,
        ISessionService sessions,
        IPacketSendService sender,
        IWorldViewService view,
        ITooltipService tooltips,
        IItemFactoryService? factory = null,
        IItemSerialPool? serials = null,
        IContainerLayoutService? layouts = null,
        IInventoryMutationGuard? inventory = null
    )
    {
        _inventory = inventory;
        _items = items;
        _sessions = sessions;
        _sender = sender;
        _view = view;
        _tooltips = tooltips;
        _factory = factory;
        _serials = serials;
        _layouts = layouts;
    }

    public ItemEntity? Make(string template, int? amount = null)
    {
        if (_factory is null || _serials is null || string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        ItemEntity item;

        try
        {
            item = _factory.Create(template, amount);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or ArgumentException)
        {
            return null;
        }

        // Taken last: an item that cannot be made must not use up a serial.
        if (!_serials.TryTake(out var serial))
        {
            return null;
        }

        item.Id = serial;

        return item;
    }

    public ItemEntity? Give(MobileEntity owner, string template, int? amount = null)
    {
        if (_inventory?.AllowsOwner(owner.Id) == false || _items.GetWorn(owner.Id).FirstOrDefault(worn => worn.Layer == LayerType.Backpack) is not { } backpack ||
            Make(template, amount) is not { } item)
        {
            return null;
        }

        var position = _layouts?.RandomGridPosition(backpack.ItemId) ?? new Point2D(44, 65);
        item.PutInContainer(backpack.Id, position, ContainerSlotUtils.FirstFree(_items.GetContents(backpack.Id)));
        _items.Add([item]);
        Refresh(item);

        return item;
    }

    public bool Consume(ItemEntity item, int amount = 1)
    {
        if (_inventory?.Allows(item) == false || amount < 1 || item.MobileId is not null || IsHeld(item) || item.Amount < amount)
        {
            return false;
        }

        if (item.Amount == amount)
        {
            return Delete(item);
        }

        item.Amount -= amount;
        Refresh(item);

        return true;
    }

    public bool Delete(ItemEntity item)
    {
        if (_inventory?.Allows(item) == false || item.MobileId is not null || IsHeld(item) || _items.GetContents(item.Id).Count > 0)
        {
            return false;
        }

        if (item.GroundLocation is not null)
        {
            _view.ItemDisappeared(item);
            _items.Absorb(item);

            return true;
        }

        if (OwnerSession(item) is { } session)
        {
            _sender.TrySend(session.SessionId, new RemoveEntityPacket(item.Id));
        }

        // Its owner's next save deletes the row, or the world save for an item nobody carries.
        _items.Absorb(item);

        return true;
    }

    public void Refresh(ItemEntity item)
    {
        if (item.GroundLocation is not null)
        {
            _view.ItemAppeared(item);
        }
        else if (OwnerSession(item) is { } session)
        {
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
            _sender.TrySend(session.SessionId, _tooltips.Info(item));
        }
    }

    // Lifted onto a player's cursor: it keeps the place it was taken from until it is dropped, so it must not be
    // drawn there again.
    public bool IsHeld(ItemEntity item)
    {
        return _sessions.GetAll().Any(session => session.Get(ItemSessionKeys.Held)?.Item == item.Id);
    }

    private GameSession? OwnerSession(ItemEntity item)
    {
        return _items.GetOwner(item) is { } owner
            ? _sessions.GetAll().FirstOrDefault(session => session.CharacterId == owner)
            : null;
    }
}
