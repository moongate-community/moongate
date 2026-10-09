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
    // The most a stack holds, as a player's drop onto a stack counts it.
    private const int MaxStack = 60_000;

    private readonly IItemService _items;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly IWorldViewService _view;
    private readonly ITooltipService _tooltips;
    private readonly IItemFactoryService? _factory;
    private readonly IItemSerialPool? _serials;
    private readonly IContainerLayoutService? _layouts;
    private readonly IInventoryMutationGuard? _inventory;
    private readonly IContainerCapacityService? _capacity;
    private readonly IItemTemplateService? _templates;
    private readonly ITileDataService? _tiles;

    public ItemHandlingService(
        IItemService items,
        ISessionService sessions,
        IPacketSendService sender,
        IWorldViewService view,
        ITooltipService tooltips,
        IItemFactoryService? factory = null,
        IItemSerialPool? serials = null,
        IContainerLayoutService? layouts = null,
        IContainerCapacityService? capacity = null,
        IInventoryMutationGuard? inventory = null,
        IItemTemplateService? templates = null,
        ITileDataService? tiles = null
    )
    {
        _templates = templates;
        _tiles = tiles;
        _inventory = inventory;
        _capacity = capacity;
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
        // Taken last: an item that cannot be made must not use up a serial.
        if (_serials is null || Create(template, amount) is not { } item || !_serials.TryTake(out var serial))
        {
            return null;
        }

        item.Id = serial;

        return item;
    }

    public ItemEntity? Give(MobileEntity owner, string template, int? amount = null, bool ignoreCapacity = false)
    {
        if (_inventory?.AllowsOwner(owner.Id) == false ||
            _items.GetWorn(owner.Id).FirstOrDefault(worn => worn.Layer == LayerType.Backpack) is not { } backpack ||
            Create(template, amount) is not { } item)
        {
            return null;
        }

        // What stacks joins the stack of its kind lying in the backpack: no new item, no slot and no serial taken.
        if (StackFor(backpack, item) is { } stack)
        {
            stack.Amount += item.Amount;
            Refresh(stack);

            return stack;
        }

        // The serial is taken last: an item that finds no room must not use one up.
        if ((!ignoreCapacity && _capacity?.HasRoomFor(backpack, 1) == false) ||
            _serials is null ||
            !_serials.TryTake(out var serial))
        {
            return null;
        }

        item.Id = serial;

        var position = _layouts?.RandomGridPosition(backpack.ItemId) ?? new Point2D(44, 65);
        item.PutInContainer(backpack.Id, position, ContainerSlotUtils.FirstFree(_items.GetContents(backpack.Id)));
        _items.Add([item]);
        Refresh(item);

        return item;
    }

    public bool Consume(ItemEntity item, int amount = 1)
    {
        if (_inventory?.Allows(item) == false || amount < 1 || item.MobileId is not null || IsHeld(item) ||
            item.Amount < amount)
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
        if (_inventory?.Allows(item) == false || item.MobileId is not null || IsHeld(item) ||
            _items.GetContents(item.Id).Count > 0)
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

    // A new item of the template with no serial yet; null when it cannot be made.
    private ItemEntity? Create(string template, int? amount)
    {
        if (_factory is null || string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        try
        {
            return _factory.Create(template, amount);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    // The stack lying in the backpack that a new item of the same kind joins, as a player's drop onto it would: same
    // template, graphic, hue, name and rarity, no prop on either, and not above the most a stack holds.
    private ItemEntity? StackFor(ItemEntity backpack, ItemEntity item)
    {
        if (_templates is null ||
            _tiles is null ||
            item.Props is { Count: > 0 } ||
            !_templates.TryGet(item.TemplateId, out var template) ||
            !template.EffectiveStackable(_tiles))
        {
            return null;
        }

        return _items.GetContents(backpack.Id)
            .FirstOrDefault(stack => stack.TemplateId == item.TemplateId &&
                                     stack.ItemId == item.ItemId &&
                                     stack.Hue == item.Hue &&
                                     stack.Name == item.Name &&
                                     stack.Rarity == item.Rarity &&
                                     stack.Props is not { Count: > 0 } &&
                                     (long)stack.Amount + item.Amount <= MaxStack
            );
    }

    private GameSession? OwnerSession(ItemEntity item)
    {
        return _items.GetOwner(item) is { } owner
            ? _sessions.GetAll().FirstOrDefault(session => session.CharacterId == owner)
            : null;
    }
}
