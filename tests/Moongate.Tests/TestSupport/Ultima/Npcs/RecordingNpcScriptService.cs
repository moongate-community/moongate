using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Npcs;

/// <summary>
///     Records the script calls it is asked to run or queue, as "Run 256 on_speech 2 hello" or "Queue 256 on_spawn".
/// </summary>
public sealed class RecordingNpcScriptService : INpcScriptService
{
    public List<string> Calls { get; } = [];

    /// <summary>
    ///     What every run throws after it is recorded; null for none.
    /// </summary>
    public Exception? Throws { get; set; }

    /// <summary>
    ///     What every run answers.
    /// </summary>
    public ScriptResult Result { get; set; } = ScriptResult.Completed([]);

    /// <summary>
    ///     What a script does while it runs, given the function's name: called before the answer is given.
    /// </summary>
    public Action<string>? OnRun { get; set; }

    public ScriptResult Run(MobileEntity npc, string function, params object?[] args)
    {
        Record("Run", npc, function, args);

        if (Throws is not null)
        {
            throw Throws;
        }

        OnRun?.Invoke(function);

        return Result;
    }

    public void Queue(MobileEntity npc, string function, params object?[] args)
    {
        Record("Queue", npc, function, args);
    }

    private void Record(string kind, MobileEntity npc, string function, object?[] args)
    {
        Calls.Add(string.Join(' ', new[] { kind, npc.Id.Value.ToString(), function }.Concat(args.Select(arg => $"{arg}"))));
    }
}
