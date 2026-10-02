using Lua;
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

    /// <summary>
    ///     Calls a function a script handed over, such as a button callback of a built gump; its coroutine belongs to
    ///     <paramref name="owner" />, the script file it came from.
    /// </summary>
    ScriptResult CallFunction(string owner, LuaFunction function, params object?[] args);

    /// <summary>
    ///     Gets whether a script is running now: calling one from the host would nest, so the work goes to the game loop.
    /// </summary>
    bool IsRunningScript { get; }

    /// <summary>
    ///     Gets the script file running now, null outside any script.
    /// </summary>
    string? CurrentScript { get; }
}
