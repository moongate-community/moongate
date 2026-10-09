using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Schedule;

public sealed class RecordingEventScriptService : IEventScriptService
{
    public List<(string Script, string Function, object?[] Args)> Calls { get; } = [];

    public ScriptResult Result { get; set; } = ScriptResult.Completed([]);

    public bool Throw { get; set; }

    public ScriptResult Call(string script, string function, params object?[] args)
    {
        Calls.Add((script, function, args));

        if (Throw)
        {
            throw new InvalidOperationException("The script failed.");
        }

        return Result;
    }
}
