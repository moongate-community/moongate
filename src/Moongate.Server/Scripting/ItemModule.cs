using MoonSharp.Interpreter;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.UO.Data.Hues;
using Moongate.Ultima.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Exposes item creation and manipulation to Lua. Items are referenced by serial (a number).
/// All functions are synchronous and must run on the game-loop thread — the single-writer boundary
/// for item state. Event handlers (<c>events.on</c>) are dispatched there automatically; other code
/// reaches it via <c>game.post</c> / <c>game.schedule</c>. Mutating calls log a warning if run off-loop.
/// </summary>
[ScriptModule("item", "Create and manipulate items by serial.")]
public sealed class ItemModule
{
    private readonly IItemFactoryService _factory;
    private readonly IItemService _items;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly ILoopThread _loopThread;

    public ItemModule(
        IItemFactoryService factory, IItemService items, IPersistenceService persistence, ILoopThread loopThread
    )
    {
        _factory = factory;
        _items = items;
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
        _loopThread = loopThread;
    }

    [ScriptFunction("create", "Creates an item from a template; returns its serial or nil.")]
    public uint? Create(string templateId, int amount, uint hue)
    {
        LoopGuard.Warn(_loopThread, "item.create");

        return Persist(_factory.CreateFromTemplate(templateId, amount: amount, hue: ToHue(hue)));
    }

    [ScriptFunction("create_by_tag", "Creates a random item carrying the tag; returns its serial or nil.")]
    public uint? CreateByTag(string tag, int amount, uint hue)
    {
        LoopGuard.Warn(_loopThread, "item.create_by_tag");

        return Persist(_factory.CreateByTag(tag, amount: amount, hue: ToHue(hue)));
    }

    [ScriptFunction("create_by_category", "Creates a random item in the category; returns its serial or nil.")]
    public uint? CreateByCategory(string category, int amount, uint hue)
    {
        LoopGuard.Warn(_loopThread, "item.create_by_category");

        return Persist(_factory.CreateByCategory(category, amount: amount, hue: ToHue(hue)));
    }

    [ScriptFunction("get", "Returns a field table for the item, or nil.")]
    public Dictionary<string, object?>? Get(uint serial)
    {
        var item = _items.GetById((Serial)serial);

        if (item is null)
        {
            return null;
        }

        return new()
        {
            ["id"] = item.Id.Value,
            ["item_id"] = item.ItemId,
            ["name"] = item.Name,
            ["amount"] = item.Amount,
            ["hue"] = (int)item.Hue.Value,
            ["layer"] = item.EquippedLayer?.ToString(),
            ["container"] = item.ParentContainerId.Value,
            ["mobile"] = item.EquippedMobileId.Value
        };
    }

    [ScriptFunction("set", "Mutates item fields from a table {amount,hue,item_id,name}; returns true on success.")]
    public bool Set(uint serial, Table fields)
    {
        LoopGuard.Warn(_loopThread, "item.set");

        var item = _items.GetById((Serial)serial);

        if (item is null || fields is null)
        {
            return false;
        }

        var amount = fields.Get("amount");

        if (amount.Type == DataType.Number)
        {
            item.Amount = (int)amount.Number;
        }

        var hue = fields.Get("hue");

        if (hue.Type == DataType.Number)
        {
            item.Hue = new Hue((ushort)hue.Number);
        }

        var itemId = fields.Get("item_id");

        if (itemId.Type == DataType.Number)
        {
            item.ItemId = (int)itemId.Number;
        }

        var name = fields.Get("name");

        if (name.Type == DataType.String)
        {
            item.Name = name.String;
        }

        _items.Save(item);

        return true;
    }

    [ScriptFunction("flip", "Flips the item to its next orientation graphic; false when it has none.")]
    public bool Flip(uint serial)
    {
        LoopGuard.Warn(_loopThread, "item.flip");

        var item = _items.GetById((Serial)serial);

        return item is not null && _items.Flip(item);
    }

    [ScriptFunction("delete", "Deletes the item; true when it existed.")]
    public bool Delete(uint serial)
    {
        LoopGuard.Warn(_loopThread, "item.delete");

        return _items.Delete((Serial)serial);
    }

    [ScriptFunction("equip", "Equips the item on a mobile at a layer; false on unknown mobile/item/layer.")]
    public bool Equip(uint mobile, uint serial, object layer)
    {
        LoopGuard.Warn(_loopThread, "item.equip");

        if (!ScriptEnums.TryResolve<LayerType>(layer, out var parsed))
        {
            return false;
        }

        var owner = _mobiles.GetById((Serial)mobile);
        var item = _items.GetById((Serial)serial);

        if (owner is null || item is null)
        {
            return false;
        }

        _items.Equip(owner, item, parsed);

        return true;
    }

    [ScriptFunction("unequip", "Removes the item on a layer; returns its serial or nil.")]
    public uint? Unequip(uint mobile, object layer)
    {
        LoopGuard.Warn(_loopThread, "item.unequip");

        if (!ScriptEnums.TryResolve<LayerType>(layer, out var parsed))
        {
            return null;
        }

        var owner = _mobiles.GetById((Serial)mobile);

        return owner is null ? null : _items.Unequip(owner, parsed)?.Id.Value;
    }

    [ScriptFunction("equipped", "Returns the serials equipped on the mobile.")]
    public List<uint> Equipped(uint mobile)
    {
        var owner = _mobiles.GetById((Serial)mobile);

        return owner is null ? [] : [.. _items.GetEquipped(owner).Select(item => item.Id.Value)];
    }

    [ScriptFunction("add_to_container", "Places an item into a container at (x, y); false on unknown serials.")]
    public bool AddToContainer(uint container, uint serial, int x, int y)
    {
        LoopGuard.Warn(_loopThread, "item.add_to_container");

        var box = _items.GetById((Serial)container);
        var item = _items.GetById((Serial)serial);

        if (box is null || item is null)
        {
            return false;
        }

        _items.AddToContainer(box, item, new Point2D(x, y));

        return true;
    }

    [ScriptFunction("remove_from_container", "Removes an item from a container; false on unknown serials.")]
    public bool RemoveFromContainer(uint container, uint serial)
    {
        LoopGuard.Warn(_loopThread, "item.remove_from_container");

        var box = _items.GetById((Serial)container);
        var item = _items.GetById((Serial)serial);

        if (box is null || item is null)
        {
            return false;
        }

        _items.RemoveFromContainer(box, item);

        return true;
    }

    [ScriptFunction("contents", "Returns the serials contained in the container.")]
    public List<uint> Contents(uint container)
        => [.. _items.GetContents((Serial)container).Select(item => item.Id.Value)];

    private uint? Persist(IReadOnlyList<ItemEntity> created)
    {
        if (created.Count == 0)
        {
            return null;
        }

        return _items.Create(created[0]).Value;
    }

    private static Hue ToHue(uint hue)
        => new((ushort)hue);
}
