using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
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

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaItemProxyTypeRegistered)
        {
            return;
        }

        UserData.RegisterType<LuaItemProxy>();
        _isLuaItemProxyTypeRegistered = true;
    }
}
