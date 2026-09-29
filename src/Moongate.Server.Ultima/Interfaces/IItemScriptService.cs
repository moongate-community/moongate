using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Calls the functions of an item's script, the global table its template names with <c>script_id</c>, defined by
///     <c>scripts/items/&lt;script_id&gt;.lua</c>, with the item's serial first. Called on the game loop, never from inside
///     a running script; nothing runs before the scripts are loaded or once they stop.
/// </summary>
public interface IItemScriptService
{
    /// <summary>
    ///     Gets whether the item's template names a script.
    /// </summary>
    bool HasScript(ItemEntity item);

    /// <summary>
    ///     Calls <paramref name="function" />; <see cref="ScriptResult.Missing" /> when the item has no script or the
    ///     script lacks it.
    /// </summary>
    ScriptResult Run(ItemEntity item, string function, params object?[] args);
}
