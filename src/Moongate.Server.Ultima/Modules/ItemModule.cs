using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules.Internal;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>item</c> Lua module: an item script reads and changes its item by serial, as
///     <c>item.consume(serial)</c>. A serial that is not a live item gives <c>false</c> or <c>nil</c>, never an error.
///     Worn items cannot be consumed or deleted yet, nor an item a player holds on the cursor, nor a container that
///     still holds items.
/// </summary>
[ScriptModule("item", "Reads and changes an item: name, amount, owner, consume, delete, message.")]
public sealed class ItemModule
{
    public const string LightProp = "light";
    public const int MaximumTextLength = 128;
    public const int MaximumAmount = 60000;

    private static readonly Hue LabelHue = new(0x03B2);

    private readonly IItemService _items;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly IWorldViewService _view;
    private readonly ITooltipService _tooltips;
    private readonly IMobileService _mobiles;
    private readonly ISpeechService _speech;
    private readonly ISectorService _sectors;
    private readonly IItemFactoryService? _factory;
    private readonly IItemSerialPool? _serials;
    private readonly IContainerLayoutService? _layouts;
    private readonly ITileDataService? _tiles;

    public ItemModule(
        IItemService items,
        ISessionService sessions,
        IPacketSendService sender,
        IWorldViewService view,
        ITooltipService tooltips,
        IMobileService mobiles,
        ISpeechService speech,
        ISectorService sectors,
        IItemFactoryService? factory = null,
        IItemSerialPool? serials = null,
        IContainerLayoutService? layouts = null,
        ITileDataService? tiles = null
    )
    {
        _factory = factory;
        _serials = serials;
        _layouts = layouts;
        _tiles = tiles;
        _items = items;
        _sessions = sessions;
        _sender = sender;
        _view = view;
        _tooltips = tooltips;
        _mobiles = mobiles;
        _speech = speech;
        _sectors = sectors;
    }

    /// <summary>
    ///     Makes a new item from a template and puts it in the backpack of <paramref name="mobile" />;
    ///     <c>item.give(user, "gold", 100)</c>. The owner sees it at once and its next save keeps it.
    /// </summary>
    [ScriptFunction(helpText: "Makes an item from a template in the mobile's backpack and gives its serial; nil for an unknown mobile or template, a mobile without a backpack or an amount the template cannot have.")]
    public long? Give(long mobile, string template, int? amount = null)
    {
        if (mobile is <= 0 or > uint.MaxValue ||
            !_mobiles.TryGet(new Serial((uint)mobile), out var owner) ||
            _items.GetWorn(owner.Id).FirstOrDefault(worn => worn.Layer == LayerType.Backpack) is not { } backpack ||
            Make(template, amount) is not { } item)
        {
            return null;
        }

        var position = _layouts?.RandomGridPosition(backpack.ItemId) ?? new Point2D(44, 65);
        item.PutInContainer(backpack.Id, position, ContainerSlotUtils.FirstFree(_items.GetContents(backpack.Id)));
        _items.Add([item]);
        Refresh(item);

        return item.Id.Value;
    }

    /// <summary>
    ///     Makes a new item from a template on the ground; <c>item.create("gold", MapType.Trammel, 1500, 1600, 10, 50)</c>.
    ///     The players around see it and the world save keeps it.
    /// </summary>
    [ScriptFunction(helpText: "Makes an item from a template on the ground at x, y, z of the map and gives its serial; nil for an unknown template, a spot outside the map, a z outside -128 to 127 or an amount the template cannot have.")]
    public long? Create(string template, MapType map, int x, int y, int z, int? amount = null)
    {
        if (z is < sbyte.MinValue or > sbyte.MaxValue || !_sectors.IsInside(map, x, y) || Make(template, amount) is not { } item)
        {
            return null;
        }

        item.PlaceOnGround(map, new Point3D(x, y, z));
        _items.Add([item]);
        _view.ItemAppeared(item);

        return item.Id.Value;
    }

    /// <summary>
    ///     Gets the id of the template the item was made from; <c>item.template(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The id of the item's template; nil for an unknown item.")]
    public string? Template(long serial)
    {
        return TryGetItem(serial, out var item) ? item.TemplateId : null;
    }

    /// <summary>
    ///     Gets the item's name, or its template id when it has none; <c>item.name(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The item's name, or its template id when it has none; nil for an unknown item.")]
    public string? Name(long serial)
    {
        return TryGetItem(serial, out var item) ? item.Name ?? item.TemplateId : null;
    }

    /// <summary>
    ///     Gets the item's amount; <c>item.amount(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The item's amount; nil for an unknown item.")]
    public int? Amount(long serial)
    {
        return TryGetItem(serial, out var item) ? item.Amount : null;
    }

    /// <summary>
    ///     Gets the serial of the mobile carrying or wearing the item; <c>item.owner(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serial of the mobile carrying or wearing the item; nil on the ground or unknown.")]
    public long? Owner(long serial)
    {
        return TryGetItem(serial, out var item) && _items.GetOwner(item) is { } owner ? owner.Value : null;
    }

    /// <summary>
    ///     Takes <paramref name="amount" /> units off the item, deleting it at 0; <c>item.consume(serial, 1)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Takes amount units (default 1) off the item, deleting it at 0; false when fewer are left or a player holds it.")]
    public bool Consume(long serial, int amount = 1)
    {
        if (amount < 1 || !TryGetItem(serial, out var item) || item.MobileId is not null || IsHeld(item) || item.Amount < amount)
        {
            return false;
        }

        if (item.Amount == amount)
        {
            return Delete(serial);
        }

        item.Amount -= amount;
        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Deletes the item; <c>item.delete(serial)</c>. A carried item's row is deleted by its owner's next save, a ground
    ///     item's by the world save.
    /// </summary>
    [ScriptFunction(helpText: "Deletes the item; false for a worn item, a held item or a container that still holds items.")]
    public bool Delete(long serial)
    {
        if (!TryGetItem(serial, out var item) ||
            item.MobileId is not null ||
            IsHeld(item) ||
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

    /// <summary>
    ///     Shows <paramref name="text" /> as a label over the item to <paramref name="player" /> only;
    ///     <c>item.message(serial, player, "You drink the potion.")</c>.
    /// </summary>
    [ScriptFunction(helpText: "A label over the item seen only by that player; false when the player is not in the world.")]
    public bool Message(long serial, long player, string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !TryGetItem(serial, out var item) ||
            player is <= 0 or > uint.MaxValue ||
            SessionOf(new Serial((uint)player)) is not { } session)
        {
            return false;
        }

        var label = new UnicodeSpeechMessagePacket(
            item.Id,
            (ushort)item.ItemId,
            SpeechType.Label,
            LabelHue,
            SpeechFontType.Normal,
            "ENU",
            string.Empty,
            text.Length > MaximumTextLength ? text[..MaximumTextLength] : text
        );

        return SpeechMessageHelper.TrySend(_sender, session, label);
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> the item keeps, saved with it across restarts;
    ///     <c>item.get_prop(serial, "vega.greeted")</c>.
    /// </summary>
    [ScriptFunction(helpText: "A value the item keeps across restarts: a string, a number or a bool; nil when it has none.")]
    public object? GetProp(long serial, string key)
    {
        return TryGetItem(serial, out var item) && item.Props is { } props && props.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    ///     Keeps <paramref name="value" /> as the prop <paramref name="key" /> of the item, saved with it by the world save,
    ///     or removes it for <c>nil</c>; <c>item.set_prop(serial, "vega.greeted", 3)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Keeps a string, a number or a bool on the item across restarts, nil removes it; false for a table, a function or a blank key.")]
    public bool SetProp(long serial, string key, object? value = null)
    {
        if (string.IsNullOrWhiteSpace(key) || !TryGetItem(serial, out var item))
        {
            return false;
        }

        if (value is null)
        {
            item.RemoveProp(key);

            return true;
        }

        if (!ScriptPropValue.TryFromLua(value, out var prop))
        {
            return false;
        }

        item.SetProp(key, prop);

        return true;
    }

    /// <summary>
    ///     Gives the item a name of its own, or with nil takes it back to its template's; <c>item.set_name(serial, "a
    ///     rusty key")</c>. The players who see the item see the new name.
    /// </summary>
    [ScriptFunction(helpText: "Sets the item's own name (cut to 128 characters), nil gives it back its template's; false for an unknown or held item.")]
    public bool SetName(long serial, string? name = null)
    {
        if (!TryGetItem(serial, out var item) || IsHeld(item))
        {
            return false;
        }

        item.Name = string.IsNullOrWhiteSpace(name) ? null :
            name.Length > MaximumTextLength ? name[..MaximumTextLength] : name;
        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Gets the item's hue; <c>item.hue(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The item's hue, 0 for the colours of its art; nil for an unknown item.")]
    public int? Hue(long serial)
    {
        return TryGetItem(serial, out var item) ? item.Hue.Value : null;
    }

    /// <summary>
    ///     Changes the item's hue; <c>item.set_hue(serial, 0x0026)</c>. The players who see the item see it change.
    /// </summary>
    [ScriptFunction(helpText: "Changes the item's hue (0 to 65535), shown to the players who see it; false for a worn or held item.")]
    public bool SetHue(long serial, int hue)
    {
        if (hue is < 0 or > ushort.MaxValue || !TryGetItem(serial, out var item) || item.MobileId is not null || IsHeld(item))
        {
            return false;
        }

        item.Hue = new Hue((ushort)hue);
        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Sets how many units the stack holds; <c>item.set_amount(serial, 20)</c>. To take units off and delete the
    ///     stack at 0, use <c>item.consume</c>.
    /// </summary>
    [ScriptFunction(helpText: "Sets the stack's amount (1 to 60000); false for an item that does not stack, a worn or held item or an amount out of range.")]
    public bool SetAmount(long serial, int amount)
    {
        if (amount is < 1 or > MaximumAmount ||
            !TryGetItem(serial, out var item) ||
            item.MobileId is not null ||
            IsHeld(item) ||
            amount > 1 && !IsStackable(item))
        {
            return false;
        }

        item.Amount = amount;
        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Gets the container the item lies in; <c>item.container(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serial of the container the item lies in; nil for an item on the ground, a worn one or an unknown one.")]
    public long? Container(long serial)
    {
        return TryGetItem(serial, out var item) && item.ContainerId is { } container ? container.Value : null;
    }

    /// <summary>
    ///     Gets the items lying directly in a container as a list of serials; <c>for _, inside in
    ///     ipairs(item.contents(bag)) do ... end</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serials of the items lying directly in the container, as a list; empty for an empty or unknown container.")]
    public LuaTable Contents(long serial)
    {
        var table = new LuaTable();

        if (TryGetItem(serial, out var container))
        {
            var index = 1;

            foreach (var inside in _items.GetContents(container.Id))
            {
                table[index++] = (long)inside.Id.Value;
            }
        }

        return table;
    }

    /// <summary>
    ///     Moves the item into a container, or into the backpack of a mobile; <c>item.move_into(serial, bag)</c>,
    ///     <c>item.move_into(serial, user)</c>. Those who saw it where it was lose it and the new owner sees it.
    /// </summary>
    [ScriptFunction(helpText: "Moves the item into a container, or into a mobile's backpack; false for a worn or held item, a target that is not a container, or a container put into itself or into what it holds.")]
    public bool MoveInto(long serial, long container)
    {
        if (!TryGetItem(serial, out var item) || item.MobileId is not null || IsHeld(item) || TargetContainer(container) is not { } target)
        {
            return false;
        }

        // Not into itself, nor into anything it holds.
        for (ItemEntity? holder = target; holder is not null; holder = ContainerOf(holder))
        {
            if (holder.Id == item.Id)
            {
                return false;
            }
        }

        if (item.GroundLocation is not null)
        {
            _view.ItemDisappeared(item);
        }
        else if (OwnerSession(item) is { } previous)
        {
            _sender.TrySend(previous.SessionId, new RemoveEntityPacket(item.Id));
        }

        _items.MoveToContainer(item, target.Id, _layouts?.RandomGridPosition(target.ItemId) ?? new Point2D(44, 65));
        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Gets the item's graphic; <c>item.item_id(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The item's graphic; nil for an unknown item.")]
    public int? ItemId(long serial)
    {
        return TryGetItem(serial, out var item) ? item.ItemId : null;
    }

    /// <summary>
    ///     Changes the item's graphic, such as a door opening; <c>item.set_item_id(serial, 0x0676)</c>. The players in
    ///     range see a ground item change, the owner an item in its containers.
    /// </summary>
    [ScriptFunction(helpText: "Changes the item's graphic (0 to 65535), shown to the players who see it; false for a worn or held item.")]
    public bool SetItemId(long serial, int graphic)
    {
        if (graphic is < 0 or > ushort.MaxValue || !TryGetItem(serial, out var item) || item.MobileId is not null || IsHeld(item))
        {
            return false;
        }

        item.ItemId = graphic;
        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Sets the shape of the light a light source gives, by LightType name, or clears it with nil;
    ///     <c>item.set_light(serial, "circle150")</c>. The players who see the item are shown it again.
    /// </summary>
    [ScriptFunction(helpText: "Sets the item's light shape by name, such as circle150 or west_big, nil clears it; false for an unknown shape or a worn or held item.")]
    public bool SetLight(long serial, string? type = null)
    {
        if (!TryGetItem(serial, out var item) || item.MobileId is not null || IsHeld(item))
        {
            return false;
        }

        if (type is null)
        {
            item.RemoveProp(LightProp);
        }
        else if (EnumNameUtils.TryParse<LightType>(type, out var light))
        {
            item.SetProp(LightProp, EnumNameUtils.Format(light));
        }
        else
        {
            return false;
        }

        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Gets where a ground item lies as <c>{ x, y, z, map }</c>; <c>item.location(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Where a ground item lies, as a table { x, y, z, map }; nil for an item not on the ground.")]
    public LuaTable? Location(long serial)
    {
        if (!TryGetItem(serial, out var item) || item.Map is not { } map || item.GroundLocation is not { } spot)
        {
            return null;
        }

        var table = new LuaTable();
        table["x"] = spot.X;
        table["y"] = spot.Y;
        table["z"] = spot.Z;
        table["map"] = (int)map;

        return table;
    }

    /// <summary>
    ///     Moves a ground item on its map, such as a door swinging; <c>item.move_to(serial, x, y, z)</c>. The players
    ///     around the old spot lose it and those around the new one see it.
    /// </summary>
    [ScriptFunction(helpText: "Moves a ground item to x, y, z on its map; false for an item not on the ground, a spot outside the map or a z outside -128 to 127.")]
    public bool MoveTo(long serial, int x, int y, int z)
    {
        // Outside the map's grid the item would be taken off its sector and never put back: seen by nobody.
        if (z is < sbyte.MinValue or > sbyte.MaxValue ||
            !TryGetItem(serial, out var item) ||
            item.Map is not { } map ||
            !_items.IsLyingOnGround(item) ||
            !_sectors.IsInside(map, x, y))
        {
            return false;
        }

        _view.ItemDisappeared(item);
        _items.PlaceOnGround(item, map, new Point3D(x, y, z));
        _view.ItemAppeared(item);

        return true;
    }

    /// <summary>
    ///     Plays <paramref name="sound" /> where the item is, on the ground or on the mobile carrying it, for the players
    ///     within 15 cells; <c>item.play_sound(serial, 0xEA)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays a sound id (0 to 65535) where the item is, on the ground or with its owner; false for an unknown item or sound.")]
    public bool PlaySound(long serial, int sound)
    {
        if (sound is < 0 or > ushort.MaxValue || !TryGetItem(serial, out var item))
        {
            return false;
        }

        if (item.Map is { } map && item.GroundLocation is { } spot)
        {
            _speech.PlaySound(map, spot, sound);

            return true;
        }

        if (_items.GetOwner(item) is { } owner && _mobiles.TryGet(owner, out var carrier))
        {
            _speech.PlaySound(carrier.Map, carrier.Location, sound);

            return true;
        }

        return false;
    }

    // A new item of the template with a serial of its own, nowhere yet; null when it cannot be made.
    private ItemEntity? Make(string template, int? amount)
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

    // The item a serial names when it is a container, or the backpack of the mobile it names.
    private ItemEntity? TargetContainer(long serial)
    {
        if (serial is <= 0 or > uint.MaxValue)
        {
            return null;
        }

        if (_mobiles.TryGet(new Serial((uint)serial), out var mobile))
        {
            return _items.GetWorn(mobile.Id).FirstOrDefault(worn => worn.Layer == LayerType.Backpack);
        }

        return _items.TryGet(new Serial((uint)serial), out var item) &&
               _tiles is not null &&
               _tiles.TryGetItem(item.ItemId, out var tile) &&
               (tile.Flags & TileFlagType.Container) != 0
            ? item
            : null;
    }

    private ItemEntity? ContainerOf(ItemEntity item)
    {
        return item.ContainerId is { } container && _items.TryGet(container, out var holder) ? holder : null;
    }

    private bool IsStackable(ItemEntity item)
    {
        return _tiles is not null && _tiles.TryGetItem(item.ItemId, out var tile) && (tile.Flags & TileFlagType.Generic) != 0;
    }

    private bool TryGetItem(long serial, [NotNullWhen(true)] out ItemEntity? item)
    {
        item = null;

        return serial is > 0 and <= uint.MaxValue && _items.TryGet(new Serial((uint)serial), out item);
    }

    // Shows a changed item again: to the players around it on the ground, or to its owner in a container.
    private void Refresh(ItemEntity item)
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
    private bool IsHeld(ItemEntity item)
    {
        return _sessions.GetAll().Any(session => session.Get(ItemSessionKeys.Held)?.Item == item.Id);
    }

    private GameSession? OwnerSession(ItemEntity item)
    {
        return _items.GetOwner(item) is { } owner ? SessionOf(owner) : null;
    }

    private GameSession? SessionOf(Serial character)
    {
        return _sessions.GetAll().FirstOrDefault(session => session.CharacterId == character);
    }
}
