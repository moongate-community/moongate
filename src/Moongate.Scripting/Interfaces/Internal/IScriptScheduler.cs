using Lua;
using Moongate.Scripting.Data.Scripts;

namespace Moongate.Scripting.Interfaces.Internal;

/// <summary>
///     What modules need from the coroutine scheduler: start a function as a coroutine on behalf of the file currently
///     running.
/// </summary>
internal interface IScriptScheduler
{
    /// <summary>
    ///     Gets the owner of the code executing right now: the running coroutine's owner during a resume, otherwise the file
    ///     being loaded; null when neither applies.
    /// </summary>
    string? CurrentOwner { get; }

    /// <summary>
    ///     Starts <paramref name="function" /> as a coroutine owned by <paramref name="owner" />.
    /// </summary>
    ScriptResult Start(LuaFunction function, string owner, params object?[] args);
}
