using Lua;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Gumps;

/// <summary>
///     Records the gump script functions called, with their arguments.
/// </summary>
public sealed class RecordingGumpScriptService : IGumpScriptService
{
    public List<(string Gump, string Function, object?[] Args)> Calls { get; } = [];

    public List<(string Gump, LuaFunction Function, object?[] Args)> FunctionCalls { get; } = [];

    /// <summary>
    ///     Gets or sets what a call does, such as filling a slot's builder; null does nothing.
    /// </summary>
    public Action<string, string, object?[]>? OnCall { get; set; }

    public ScriptResult Call(string gumpId, string function, params object?[] args)
    {
        Calls.Add((gumpId, function, args));
        OnCall?.Invoke(gumpId, function, args);

        return ScriptResult.Missing;
    }

    public ScriptResult CallFunction(string gumpId, LuaFunction function, params object?[] args)
    {
        FunctionCalls.Add((gumpId, function, args));

        return ScriptResult.Completed([]);
    }
}
