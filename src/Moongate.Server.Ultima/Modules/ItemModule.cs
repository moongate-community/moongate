using Moongate.Server.Ultima.Interfaces.Items;
using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
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
///     The <c>item</c> Lua module: an item script reads and changes its item by serial, as <c>item.consume(serial)</c>.
///     A serial that is not a live item gives <c>false</c> or <c>nil</c>, never an error.
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
    private readonly IItemHandlingService _handling;
    private readonly IItemSerialPool? _serials;
    private readonly IContainerLayoutService? _layouts;
    private readonly ITileDataService? _tiles;
    private readonly IItemTemplateService? _templates;
    private readonly ILootService? _loot;
    private readonly IEquipmentService? _equipment;
    private readonly IItemTimerService? _timers;

    private readonly IInventoryMutationGuard? _inventory;
    private readonly IContainerViewService? _views;

    public ItemModule(
        IItemService items,
        ISessionService sessions,
        IPacketSendService sender,
        IWorldViewService view,
        ITooltipService tooltips,
        IMobileService mobiles,
        ISpeechService speech,
        ISectorService sectors,
        IItemHandlingService handling,
        IItemSerialPool? serials = null,
        IContainerLayoutService? layouts = null,
        ITileDataService? tiles = null,
        IItemTemplateService? templates = null,
        ILootService? loot = null,
        IEquipmentService? equipment = null,
        IItemTimerService? timers = null,
        IInventoryMutationGuard? inventory = null,
        IContainerViewService? views = null
    )
    {
        _views = views;
        _inventory = inventory;
        _timers = timers;
        _equipment = equipment;
        _loot = loot;
        _templates = templates;
        _handling = handling;
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
    [ScriptFunction(
        helpText:
        "Makes an item from a template in the mobile's backpack and gives its serial; what stacks joins the stack of its kind already lying in the backpack (same template, hue and name, no prop of its own, 60000 at most) and the serial is that stack's. The owner sees it at once and its next save keeps it. Nil for an unknown mobile or template, a mobile without a backpack, an amount the template cannot have (more than 1 of what does not stack), or when no serial is ready: the server keeps 64 in reserve and refills them in the background, so making more in one go gives nil for the rest; try again later."
    )]
    public long? Give(long mobile, string template, int? amount = null)
    {
        return mobile is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)mobile), out var owner) &&
               _handling.Give(owner, template, amount) is { } item
            ? item.Id.Value
            : null;
    }

    /// <summary>
    ///     Rolls a loot table of <c>templates/loots</c> once, or <paramref name="rolls" /> times, and puts what it
    ///     gives
    ///     into a container, or into the backpack of a mobile; <c>item.add_loot(chest, "fillable_baker")</c>,
    ///     <c>item.add_loot(chest, "fillable_baker", 4)</c>. A roll may give nothing.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Rolls a loot table of templates/loots once, or rolls times, into a container, or into the backpack of a mobile, and gives how many items it added; 0 for rolls that give nothing, an unknown table or something that is no container; fewer than the roll gave when the container is full (125 items) or no item serial is at hand."
    )]
    public int AddLoot(long container, string table, int rolls = 1)
    {
        if (_loot is null ||
            _serials is null ||
            rolls < 1 ||
            string.IsNullOrWhiteSpace(table) ||
            !_loot.TryGet(table, out _) ||
            TargetContainer(container) is not { } target)
        {
            return 0;
        }

        if (_inventory?.Allows(target) == false)
        {
            return 0;
        }

        var contents = _items.GetContents(target.Id).ToList();
        var added = 0;

        // The contents are read once for all the rolls.
        foreach (var item in Enumerable.Range(0, rolls).SelectMany(_ => _loot.Roll(table)))
        {
            // The pool is small: what it cannot name is left out.
            if (contents.Count >= ContainerSlotUtils.SlotCount || !_serials.TryTake(out var serial))
            {
                break;
            }

            item.Id = serial;
            item.PutInContainer(
                target.Id,
                _layouts?.RandomGridPosition(target.ItemId) ?? new Point2D(44, 65),
                ContainerSlotUtils.FirstFree(contents)
            );
            _items.Add([item]);
            contents.Add(item);
            added++;

            // A carried container shows its new item; one on the ground shows its contents when it is opened.
            if (OwnerSession(item) is not null)
            {
                Refresh(item);
            }
        }

        return added;
    }

    /// <summary>
    ///     Makes a new item from a template on the ground;
    ///     <c>item.create("gold", MapType.Trammel, 1500, 1600, 10, 50)</c>.
    ///     The players around see it and the world save keeps it.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Makes an item from a template on the ground at x, y, z of the map, seen by the players around, and gives its serial; nil for an unknown template, a spot outside the map, a z outside -128 to 127, an amount the template cannot have, or when no serial is ready, as item.give."
    )]
    public long? Create(string template, MapType map, int x, int y, int z, int? amount = null)
    {
        if (z is < sbyte.MinValue or > sbyte.MaxValue || !_sectors.IsInside(map, x, y) ||
            Make(template, amount) is not { } item)
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
    ///     Gets the script of the item's template; <c>item.script(serial) == "dye_tub"</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The script_id of the item's template, such as 'door' or 'dye_tub'; nil for an item whose template has no script, or an unknown item."
    )]
    public string? Script(long serial)
    {
        return TryGetTemplate(serial, out var template) && !string.IsNullOrEmpty(template.ScriptId)
            ? template.ScriptId
            : null;
    }

    /// <summary>
    ///     Gets whether a dye tub can give the item its hue; <c>item.dyeable(serial)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the item's template is dyeable, as clothing is: a dye tub can give it its hue; false for an unknown item."
    )]
    public bool Dyeable(long serial)
    {
        return TryGetTemplate(serial, out var template) && template.Dyeable == true;
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
    ///     Gets the serial of the mobile wearing or holding the item itself, such as an axe in its hands;
    ///     <c>item.worn_by(serial) == user</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The serial of the mobile that wears the item or holds it in its hands (a backpack is worn too); nil for an item inside a backpack or another container, on the ground, lifted onto a player's cursor or unknown."
    )]
    public long? WornBy(long serial)
    {
        // An item lifted off the paperdoll is still its wearer's until it is dropped: it is in nobody's hands.
        return TryGetItem(serial, out var item) && item.MobileId is { } wearer && !IsHeld(item) ? wearer.Value : null;
    }

    /// <summary>
    ///     Takes <paramref name="amount" /> units off the item, deleting it at 0; <c>item.consume(serial, 1)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Takes amount units (default 1) off the item, deleting it at 0, and shows the change to the owner or the players around a ground stack; false for a worn item, an amount below 1, fewer units left or an item a player holds on the cursor."
    )]
    public bool Consume(long serial, int amount = 1)
    {
        return TryGetItem(serial, out var item) && _handling.Consume(item, amount);
    }

    /// <summary>
    ///     Deletes the item; <c>item.delete(serial)</c>. A carried item's row is deleted by its owner's next save, a
    ///     ground
    ///     item's by the world save.
    /// </summary>
    [ScriptFunction(helpText: "Deletes the item; false for a worn item, a held item or a container that still holds items.")]
    public bool Delete(long serial)
    {
        return TryGetItem(serial, out var item) && _handling.Delete(item);
    }

    /// <summary>
    ///     Shows a text of the client, by its number, as a label over the item to <paramref name="player" /> only, in
    ///     the language of that client; <c>item.message_cliloc(serial, player, 1042958, "3:05")</c>. The arguments fill
    ///     the <c>~1_NAME~</c> places of the text, split by tabs.
    /// </summary>
    [ScriptFunction(
        helpText:
        "A label over the item seen only by that player, with a text of the client by its number, in the client's language; args fill its ~1_NAME~ places, split by tabs. False when the player or the item is not in the world."
    )]
    public bool MessageCliloc(long serial, long player, int cliloc, string? args = null)
    {
        if (cliloc <= 0 ||
            !TryGetItem(serial, out var item) ||
            player is <= 0 or > uint.MaxValue ||
            SessionOf(new Serial((uint)player)) is not { } session)
        {
            return false;
        }

        return _sender.TrySend(session.SessionId, new LocalizedMessagePacket(item.Id, item.ItemId, cliloc, "", args ?? ""));
    }

    /// <summary>
    ///     Shows <paramref name="text" /> as a label over the item to <paramref name="player" /> only;
    ///     <c>item.message(serial, player, "You drink the potion.")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "A label over the item seen only by that player (cut to 128 characters); false for blank text, an unknown item or a player not in the world."
    )]
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
        return TryGetItem(serial, out var item) && item.Props is { } props && props.TryGetValue(key, out var value)
            ? value
            : null;
    }

    /// <summary>
    ///     Keeps <paramref name="value" /> as the prop <paramref name="key" /> of the item, saved with it by the world save,
    ///     or removes it for <c>nil</c>; <c>item.set_prop(serial, "vega.greeted", 3)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Keeps a string, a number or a bool on the item across restarts, saved by the world save or its owner's save; nil removes it. False for a table, a function, a blank key or a timer.<name> key, which the item's timers use."
    )]
    public bool SetProp(long serial, string key, object? value = null)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

        // The timer props are the item's timers: only item.start_timer and item.stop_timer write them.
        if (string.IsNullOrWhiteSpace(key) ||
            key.StartsWith(ItemTimerQueue.PropPrefix, StringComparison.Ordinal) ||
            !TryGetItem(serial, out var item))
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
    ///     Gives the item a name of its own, or with nil takes it back to its template's;
    ///     <c>item.set_name(serial, "a rusty key")</c>. The players who see the item see the new name.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets the item's own name (cut to 128 characters), nil gives it back its template's, shown to the players who see it; false for a worn or held item."
    )]
    public bool SetName(long serial, string? name = null)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

        if (!TryGetItem(serial, out var item) || item.MobileId is not null || IsHeld(item))
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
    [ScriptFunction(
        helpText: "Changes the item's hue (0 to 65535), shown to the players who see it; false for a worn or held item."
    )]
    public bool SetHue(long serial, int hue)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

        if (hue is < 0 or > ushort.MaxValue || !TryGetItem(serial, out var item) || item.MobileId is not null ||
            IsHeld(item))
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
    [ScriptFunction(
        helpText:
        "Sets the stack's amount (1 to 60000), shown to the players who see it; false for a worn or held item, an amount out of range, or an amount above 1 on an item that does not stack, as its template or its graphic says."
    )]
    public bool SetAmount(long serial, int amount)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

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
    [ScriptFunction(
        helpText:
        "The serial of the container the item lies in; nil for an item on the ground, a worn one or an unknown one."
    )]
    public long? Container(long serial)
    {
        return TryGetItem(serial, out var item) && item.ContainerId is { } container ? container.Value : null;
    }

    /// <summary>
    ///     Gets the items lying directly in a container as a list of serials;
    ///     <c>for _, inside in ipairs(item.contents(bag)) do ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The serials of the items lying directly in the container, as a list; empty for an empty or unknown container."
    )]
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
    [ScriptFunction(
        helpText:
        "Moves the item into a container, or into a mobile's backpack; those who saw it lose it and the new owner sees it. No weight or item limit is checked. False for a worn or held item, a target that is not a container, a container put into itself or into what it holds, or an item one mobile carries moved to another mobile (a trade, not supported yet)."
    )]
    public bool MoveInto(long serial, long container)
    {
        if (!TryGetItem(serial, out var item) || item.MobileId is not null || IsHeld(item) ||
            TargetContainer(container) is not { } target)
        {
            return false;
        }

        if (_inventory?.Allows(item, target.Id) == false)
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

        // From one mobile to another is a trade, which the saves do not follow yet: the old owner's row could bring
        // the item back.
        var previousOwner = _items.GetOwner(item);
        var newOwner = _items.GetOwner(target);

        if (previousOwner is not null && newOwner is not null && previousOwner != newOwner)
        {
            return false;
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

        // Its row still says the old owner carries it: that owner's leave saves where it lies now.
        if (previousOwner is { } owner && newOwner is null)
        {
            _items.Release(item, owner);
        }

        Refresh(item);

        return true;
    }

    /// <summary>
    ///     Puts the item on a mobile, on the layer its template gives it; <c>item.equip(serial, user)</c>. The layer
    ///     must
    ///     be free. Those who saw the item where it was lose it and everyone around sees it worn; its script's
    ///     <c>can_equip</c> is not asked.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Puts the item on the mobile, on its template's layer, seen by everyone around; its script runs on_equip, its can_equip is not asked. False for a worn or held item, a stack, an item without a layer or one the mobile cannot wear, a taken layer, a mobile not in the world, or an item another mobile carries."
    )]
    public bool Equip(long serial, long mobile)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded, new Serial((uint)mobile)) == false)
        {
            return false;
        }

        if (_equipment is null ||
            !TryGetItem(serial, out var item) ||
            item.MobileId is not null ||
            item.Amount != 1 ||
            IsHeld(item) ||
            mobile is <= 0 or > uint.MaxValue ||
            !_mobiles.TryGet(new Serial((uint)mobile), out var wearer) ||
            // From one mobile to another is a trade, which the saves do not follow yet.
            (_items.GetOwner(item) is { } owner && owner != wearer.Id) ||
            !_equipment.TryGetLayer(item, out var layer) ||
            !_equipment.CanWear(wearer.Id, item, layer))
        {
            return false;
        }

        if (item.GroundLocation is not null)
        {
            _view.ItemDisappeared(item);
        }
        else if (OwnerSession(item) is { } previous)
        {
            _sender.TrySend(previous.SessionId, new RemoveEntityPacket(item.Id));
        }
        else if (_items.GetGroundRoot(item) is { } chest)
        {
            // Those who look into the chest on the ground see it go.
            _view.ContainedItemDisappeared(item, chest, Serial.Zero);
        }

        _items.Equip(item, wearer.Id, layer);
        _view.WornItemChanged(wearer, item);

        return true;
    }

    /// <summary>
    ///     Gets the items of a template inside a container, at any depth, or among everything a mobile wears and
    ///     carries; <c>for _, coins in ipairs(item.find(user, "gold")) do ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The serials of the items of a template inside a container, at any depth, or worn and carried by a mobile (not what lies in its bank), as a list; empty when there is none, or for an unknown container or mobile."
    )]
    public LuaTable Find(long holder, string template)
    {
        var table = new LuaTable();

        if (holder is <= 0 or > uint.MaxValue || string.IsNullOrEmpty(template))
        {
            return table;
        }

        var serial = new Serial((uint)holder);
        var index = 1;

        // On a mobile, what lies in the bank is not carried, as world.carries.
        var held = serial.IsItem
            ? Inside(serial)
            : _items.GetOwnedBy(serial).Where(item => _items.GetWornRoot(item)?.Layer != LayerType.Bank);

        foreach (var inside in held)
        {
            if (inside.TemplateId == template)
            {
                table[index++] = (long)inside.Id.Value;
            }
        }

        return table;
    }

    /// <summary>
    ///     Starts a timer the item keeps, or starts it again from now; <c>item.start_timer(serial, "close", 20)</c>.
    ///     When
    ///     its time comes the item's script runs <c>on_timer(serial, name)</c>. It is saved with the item, so it also
    ///     runs after a restart.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Starts, or starts again from now, a timer of the item that runs on_timer(serial, name) of its script after that many seconds, also after a restart; one whose script has no on_timer, or fails, is dropped with a warning in the log. False for an unknown item, a blank name or one over 32 characters, or seconds not above 0 or over a year."
    )]
    public bool StartTimer(long serial, string name, double seconds)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

        return _timers is not null &&
               TryGetItem(serial, out var item) &&
               double.IsFinite(seconds) &&
               seconds <= ItemTimerService.MaximumDelay.TotalSeconds &&
               _timers.Start(item, name, TimeSpan.FromSeconds(Math.Max(0, seconds)));
    }

    /// <summary>
    ///     Stops a timer of the item; <c>item.stop_timer(serial, "close")</c>.
    /// </summary>
    [ScriptFunction(helpText: "Stops a timer of the item; false for an unknown item or when it has no timer of that name.")]
    public bool StopTimer(long serial, string name)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

        return _timers is not null && TryGetItem(serial, out var item) && _timers.Stop(item, name);
    }

    /// <summary>
    ///     Gets the seconds a timer of the item still has to run; <c>item.timer(serial, "close")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The seconds a timer of the item still has to run, 0 when it is due; nil for an unknown item or when it has no timer of that name."
    )]
    public double? Timer(long serial, string name)
    {
        return _timers is not null && TryGetItem(serial, out var item) ? _timers.Remaining(item, name)?.TotalSeconds : null;
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
    [ScriptFunction(
        helpText:
        "Changes the item's graphic (0 to 65535), as a door opening, shown to the players around a ground item or the owner of a carried one; an item inside a container on the ground changes without being shown again. False for an unknown, worn or held item or a graphic out of range."
    )]
    public bool SetItemId(long serial, int graphic)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

        if (graphic is < 0 or > ushort.MaxValue || !TryGetItem(serial, out var item) || item.MobileId is not null ||
            IsHeld(item))
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
    [ScriptFunction(
        helpText:
        "Sets the item's light shape by LightType name, such as circle150, circle300 or west_big, nil clears it; the players who see the item are shown it again, and the client draws the light only for a lit graphic. False for an unknown shape or a worn or held item."
    )]
    public bool SetLight(long serial, string? type = null)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

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
    ///     Gets whether a mobile is on the map of a ground item and within <paramref name="range" /> tiles of it, as
    ///     the view range counts them; <c>item.in_range(serial, user, 2)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the mobile is on the map of a ground item and within range tiles of it, the larger of the two differences; false for an item not on the ground, a mobile not in the world or a negative range."
    )]
    public bool InRange(long serial, long mobile, int range)
    {
        if (range < 0 ||
            mobile is <= 0 or > uint.MaxValue ||
            !TryGetItem(serial, out var item) ||
            item.Map is not { } map ||
            item.GroundLocation is not { } spot ||
            !_mobiles.TryGet(new Serial((uint)mobile), out var who) ||
            who.Map != map)
        {
            return false;
        }

        return Math.Abs(who.Location.X - spot.X) <= range && Math.Abs(who.Location.Y - spot.Y) <= range;
    }

    /// <summary>
    ///     Moves a ground item on its map, such as a door swinging; <c>item.move_to(serial, x, y, z)</c>. The players
    ///     around the old spot lose it and those around the new one see it.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Moves a ground item to x, y, z on its map: the players around the old spot lose it, those around the new one see it, and a decaying item's decay starts again; false for an item not on the ground, a spot outside the map or a z outside -128 to 127."
    )]
    public bool MoveTo(long serial, int x, int y, int z)
    {
        if (TryGetItem(serial, out var guarded) && _inventory?.Allows(guarded) == false)
        {
            return false;
        }

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
    [ScriptFunction(
        helpText:
        "Plays a sound id (0 to 65535) where the item lies, or where the mobile carrying it stands, for the players within 15 cells; false for an unknown item, a sound out of range or an item inside a container on the ground."
    )]
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
        return _handling.Make(template, amount);
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

    // Everything inside a container, at any depth; each container once, whatever the data says.
    private IEnumerable<ItemEntity> Inside(Serial container)
    {
        var pending = new Queue<Serial>([container]);
        var seen = new HashSet<Serial> { container };

        while (pending.TryDequeue(out var current))
        {
            foreach (var inside in _items.GetContents(current))
            {
                if (seen.Add(inside.Id))
                {
                    yield return inside;
                    pending.Enqueue(inside.Id);
                }
            }
        }
    }

    private ItemEntity? ContainerOf(ItemEntity item)
    {
        return item.ContainerId is { } container && _items.TryGet(container, out var holder) ? holder : null;
    }

    // As the item factory: the template's word, else the tiledata of the graphic.
    private bool IsStackable(ItemEntity item)
    {
        if (_tiles is null)
        {
            return false;
        }

        if (_templates is not null && _templates.TryGet(item.TemplateId, out var template))
        {
            return template.EffectiveStackable(_tiles);
        }

        return _tiles.TryGetItem(item.ItemId, out var tile) && (tile.Flags & TileFlagType.Generic) != 0;
    }

    private bool TryGetTemplate(long serial, [NotNullWhen(true)] out ItemTemplate? template)
    {
        template = null;

        return TryGetItem(serial, out var item) && _templates is not null &&
               _templates.TryGet(item.TemplateId, out template);
    }

    private bool TryGetItem(long serial, [NotNullWhen(true)] out ItemEntity? item)
    {
        item = null;

        return serial is > 0 and <= uint.MaxValue && _items.TryGet(new Serial((uint)serial), out item);
    }

    // Shows a changed item again: to the players around it on the ground, or to its owner in a container.
    private void Refresh(ItemEntity item)
    {
        _handling.Refresh(item);
    }

    // Lifted onto a player's cursor: it keeps the place it was taken from until it is dropped, so it must not be
    // drawn there again.
    private bool IsHeld(ItemEntity item)
    {
        return _handling.IsHeld(item);
    }

    private GameSession? OwnerSession(ItemEntity item)
    {
        return _items.GetOwner(item) is { } owner ? SessionOf(owner) : null;
    }

    /// <summary>
    ///     Shows a container, and what is directly in it, to a player; <c>item.show_contents(backpack, user)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens the container on the player's client, with the items directly inside it, as a double click would, with no check of whether the player may: the script has decided. False for a serial that is not an item container, or a player not in the world."
    )]
    public bool ShowContents(long container, long player)
    {
        if (_views is null ||
            !TryGetItem(container, out var item) ||
            player is <= 0 or > uint.MaxValue ||
            SessionOf(new Serial((uint)player)) is not { } session ||
            _tiles is null ||
            !_tiles.TryGetItem(item.ItemId, out var tile) ||
            (tile.Flags & TileFlagType.Container) == 0)
        {
            return false;
        }

        _views.Show(session, item);

        return true;
    }

    private GameSession? SessionOf(Serial character)
    {
        return _sessions.GetAll().FirstOrDefault(session => session.CharacterId == character);
    }
}
