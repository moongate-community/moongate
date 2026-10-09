using Moongate.Scripting.Data.Scripts;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Calls the scripts of
///     <c>
///         scripts/events
///     </c>
///     : the hooks of the seasonal events and the Lua tasks of the schedule.
/// </summary>
public interface IEventScriptService
{
    /// <summary>
    ///     Calls <paramref name="function" /> of the global table <paramref name="script" />, the one
    ///     <c>
    ///         scripts/events/&lt;script&gt;.lua
    ///     </c>
    ///     defines.
    /// </summary>
    /// <param name="script">
    ///     The name of the file without its extension, which is also the name of its table.
    /// </param>
    /// <param name="function">
    ///     The function to call.
    /// </param>
    /// <param name="args">
    ///     The arguments of the call.
    /// </param>
    /// <returns>
    ///     What the call gave; Missing when there is no such script or function, or the service is not running.
    /// </returns>
    ScriptResult Call(string script, string function, params object?[] args);
}
