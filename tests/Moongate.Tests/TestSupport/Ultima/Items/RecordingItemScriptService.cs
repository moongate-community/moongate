using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Gives a script to the items whose template id is in <see cref="Scripted" />, records the calls run as
///     "0x40000003 on_use 2" in <see cref="Calls" /> and the queued ones in <see cref="Queued" /> and answers them with
///     <see cref="Result" />.
/// </summary>
public sealed class RecordingItemScriptService : IItemScriptService
{
    public HashSet<string> Scripted { get; } = [];

    public ScriptResult Result { get; set; } = ScriptResult.Completed([]);

    /// <summary>
    ///     The functions that answer <c>false</c>, such as <c>can_pick_up</c>, whatever <see cref="Result" /> is.
    /// </summary>
    public HashSet<string> Refused { get; } = [];

    /// <summary>
    ///     What a script does while it runs, given the function's name: called before the answer is given.
    /// </summary>
    public Action<string>? OnRun { get; set; }

    public List<string> Calls { get; } = [];

    public List<string> Queued { get; } = [];

    public bool HasScript(ItemEntity item)
    {
        return Scripted.Contains(item.TemplateId);
    }

    public bool Has(ItemEntity item, string function)
    {
        return HasScript(item) && !NoFunctions.Contains(function);
    }

    /// <summary>
    ///     The functions a scripted item does not have, as <see cref="Has" /> answers.
    /// </summary>
    public HashSet<string> NoFunctions { get; } = [];

    public ScriptResult Run(ItemEntity item, string function, params object?[] args)
    {
        if (!HasScript(item))
        {
            return ScriptResult.Missing;
        }

        Calls.Add(Describe(item, function, args));
        OnRun?.Invoke(function);

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
