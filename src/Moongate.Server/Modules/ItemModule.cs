using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Items;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.UO.Data.Ids;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("item", "Provides helpers to resolve items from scripts.")]
/// <summary>
/// Exposes item lookup helpers to Lua scripts.
/// </summary>
public sealed class ItemModule
{
    private static bool _isLuaItemRefTypeRegistered;
    private readonly IItemService _itemService;

    public ItemModule(IItemService itemService)
        => _itemService = itemService;

    [ScriptFunction("get", "Gets an item reference by item id, or nil when not found.")]
    public LuaItemRef? Get(uint itemId)
    {
        if (itemId == 0)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var item = _itemService.GetItemAsync((Serial)itemId).GetAwaiter().GetResult();

        return item is null ? null : new(item);
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaItemRefTypeRegistered)
        {
            return;
        }

        UserData.RegisterType<LuaItemRef>();
        _isLuaItemRefTypeRegistered = true;
    }
}
