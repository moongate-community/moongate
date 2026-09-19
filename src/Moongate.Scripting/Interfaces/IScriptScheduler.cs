using Lua;
using Moongate.Scripting.Data.Scripts;

namespace Moongate.Scripting.Interfaces;

/// <summary>What modules need from the coroutine scheduler: start a function as a coroutine on behalf of the file currently running.</summary>
internal interface IScriptScheduler
{
    /// <summary>Gets the file whose code is executing, used as the owner of anything it schedules; null outside a file load.</summary>
    string? CurrentOwner { get; }

    /// <summary>Starts <paramref name="function"/> as a coroutine owned by <paramref name="owner"/>.</summary>
    ScriptResult Start(LuaFunction function, string owner, params object?[] args);
}
