using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("item", "Provides helpers to resolve items from scripts.")]

/// <summary>
/// Exposes item lookup helpers to Lua scripts.
/// </summary>
public sealed class ItemModule
{
    private static bool _isLuaItemProxyTypeRegistered;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly ISpeechService? _speechService;

    public ItemModule(
        IItemService itemService,
        ISpatialWorldService? spatialWorldService = null,
        ISpeechService? speechService = null
    )
    {
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _speechService = speechService;
    }

    [ScriptFunction("get", "Gets an item reference by item id, or nil when not found.")]
    public LuaItemProxy? Get(uint itemId)
    {
        if (itemId == 0)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var item = _itemService.GetItemAsync((Serial)itemId).GetAwaiter().GetResult();

        return item is null ? null : new(item, _itemService, _spatialWorldService, _speechService);
    }

    [ScriptFunction("spawn", "Spawns an item template at world position { x, y, z, map_id }.")]
    public LuaItemProxy? Spawn(string itemTemplateId, Table? position, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemTemplateId) ||
            amount <= 0 ||
            !TryParsePosition(position, out var location, out var mapId))
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var item = _itemService.SpawnFromTemplateAsync(itemTemplateId.Trim()).GetAwaiter().GetResult();

        if (amount > 1)
        {
            item.Amount = amount;
            _itemService.UpsertItemAsync(item).GetAwaiter().GetResult();
        }

        var moved = _itemService.MoveItemToWorldAsync(item.Id, location, mapId).GetAwaiter().GetResult();

        if (!moved)
        {
            return null;
        }

        var resolved = _itemService.GetItemAsync(item.Id).GetAwaiter().GetResult() ?? item;

        return new(resolved, _itemService, _spatialWorldService, _speechService);
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaItemProxyTypeRegistered)
        {
            return;
        }

        var type = typeof(LuaItemProxy);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
        _isLuaItemProxyTypeRegistered = true;
    }

    private static bool TryGetRequiredInt(Table table, string key, out int value)
    {
        value = 0;
        var dyn = table.Get(key);

        switch (dyn.Type)
        {
            case DataType.Number:
                value = (int)dyn.Number;

                return true;
            case DataType.String when int.TryParse(dyn.String, out var parsed):
                value = parsed;

                return true;
            default:
                return false;
        }
    }

    private static bool TryParsePosition(Table? position, out Point3D location, out int mapId)
    {
        location = Point3D.Zero;
        mapId = 0;

        if (position is null)
        {
            return false;
        }

        if (!TryGetRequiredInt(position, "x", out var x) ||
            !TryGetRequiredInt(position, "y", out var y) ||
            !TryGetRequiredInt(position, "z", out var z) ||
            !TryGetRequiredInt(position, "map_id", out mapId))
        {
            return false;
        }

        location = new(x, y, z);

        return true;
    }
}
