using Moongate.Scripting.Types.Scripts;

namespace Moongate.Scripting.Data.Scripts;

/// <summary>Outcome of a call into Lua.</summary>
public sealed class ScriptResult
{
    public static ScriptResult Suspended { get; } = new(ScriptResultKind.Suspended, [], null);

    public ScriptResultKind Kind { get; }
    public IReadOnlyList<object?> Values { get; }
    public ScriptErrorInfo? Error { get; }

    private ScriptResult(ScriptResultKind kind, IReadOnlyList<object?> values, ScriptErrorInfo? error)
    {
        Kind = kind;
        Values = values;
        Error = error;
    }

    public static ScriptResult Completed(IReadOnlyList<object?> values)
    {
        return new ScriptResult(ScriptResultKind.Completed, values, null);
    }

    public static ScriptResult Failed(ScriptErrorInfo error)
    {
        return new ScriptResult(ScriptResultKind.Failed, [], error);
    }
}
