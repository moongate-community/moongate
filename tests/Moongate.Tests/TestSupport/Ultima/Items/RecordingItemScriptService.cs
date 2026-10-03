using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Gives a script to the items whose template id is in <see cref="Scripted" />, records the calls run as
///     "0x40000003 on_use 2" in <see cref="Calls" /> and the queued ones in <see cref="Queued" /> and answers them with <see cref="Result" />.
/// </summary>
public sealed class RecordingItemScriptService : IItemScriptService
{
    public HashSet<string> Scripted { get; } = [];

    public ScriptResult Result { get; set; } = ScriptResult.Completed([]);

    /// <summary>
    ///     The functions that answer <c>false</c>, such as <c>can_pick_up</c>, whatever <see cref="Result" /> is.
    /// </summary>
    public HashSet<string> Refused { get; } = [];

    public List<string> Calls { get; } = [];

    public List<string> Queued { get; } = [];

    public bool HasScript(ItemEntity item)
    {
        return Scripted.Contains(item.TemplateId);
    }

    public ScriptResult Run(ItemEntity item, string function, params object?[] args)
    {
        if (!HasScript(item))
        {
            return ScriptResult.Missing;
        }

        Calls.Add(Describe(item, function, args));

        return Refused.Contains(function) ? ScriptResult.Completed([false]) : Result;
    }

    public void Queue(ItemEntity item, string function, params object?[] args)
    {
        if (HasScript(item))
        {
            Queued.Add(Describe(item, function, args));
        }
    }

    private static string Describe(ItemEntity item, string function, object?[] args)
    {
        return string.Join(' ', new[] { $"0x{item.Id.Value:X8}", function }.Concat(args.Select(arg => $"{arg}")));
    }
}
