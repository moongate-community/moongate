using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Gumps;

/// <summary>
///     Records the gump script functions called, with their arguments.
/// </summary>
public sealed class RecordingGumpScriptService : IGumpScriptService
{
    public List<(string Gump, string Function, object?[] Args)> Calls { get; } = [];

    public ScriptResult Call(string gumpId, string function, params object?[] args)
    {
        Calls.Add((gumpId, function, args));

        return ScriptResult.Missing;
    }
}
