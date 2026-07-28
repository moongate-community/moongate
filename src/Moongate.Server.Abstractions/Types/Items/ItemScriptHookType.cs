namespace Moongate.Server.Abstractions.Types.Items;

/// <summary>
/// The moments an item script can react to. Each maps to one Lua function name on the script's
/// table — see <c>LuaItemScriptRuntime.GetHookName</c>.
/// </summary>
public enum ItemScriptHookType
{
    SingleClick,
    DoubleClick,
    Dropped,
    Equipped,
    Unequipped
}
