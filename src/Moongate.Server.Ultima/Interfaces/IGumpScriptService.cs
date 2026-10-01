using Moongate.Scripting.Data.Scripts;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Calls the functions of a gump's script, the global table named after the gump id, defined by
///     <c>scripts/gumps/&lt;id&gt;.lua</c>. Called on the game loop; nothing runs before the scripts are loaded or once
///     they stop.
/// </summary>
public interface IGumpScriptService
{
    /// <summary>
    ///     Calls <paramref name="function" /> of gump <paramref name="gumpId" />; <see cref="ScriptResult.Missing" /> when
    ///     the gump has no script or the script lacks it.
    /// </summary>
    ScriptResult Call(string gumpId, string function, params object?[] args);
}
