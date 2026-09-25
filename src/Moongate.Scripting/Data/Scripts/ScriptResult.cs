using Moongate.Scripting.Types.Scripts;

namespace Moongate.Scripting.Data.Scripts;

/// <summary>Outcome of a call into Lua.</summary>
public sealed class ScriptResult
{
    /// <summary>
    /// Gets the shared result of a call that suspended on <c>wait</c>: the coroutine is parked on the timer wheel and will
    /// be resumed there, so it carries no values and no error.
    /// </summary>
    public static ScriptResult Suspended { get; } = new(ScriptResultKind.Suspended, [], null);

    /// <summary>Gets how the call ended.</summary>
    public ScriptResultKind Kind { get; }

    /// <summary>
    /// Gets the values the function returned, converted to CLR types. Empty unless <see cref="Kind" /> is
    /// <see cref="ScriptResultKind.Completed" />.
    /// </summary>
    public IReadOnlyList<object?> Values { get; }

    /// <summary>Gets the failure, or null unless <see cref="Kind" /> is <see cref="ScriptResultKind.Failed" />.</summary>
    public ScriptErrorInfo? Error { get; }

    private ScriptResult(ScriptResultKind kind, IReadOnlyList<object?> values, ScriptErrorInfo? error)
    {
        Kind = kind;
        Values = values;
        Error = error;
    }

    /// <summary>Creates the result of a function that ran to its end.</summary>
    /// <param name="values">The values it returned, converted to CLR types.</param>
    /// <returns>A <see cref="ScriptResultKind.Completed" /> result carrying <paramref name="values" />.</returns>
    public static ScriptResult Completed(IReadOnlyList<object?> values)
    {
        return new(ScriptResultKind.Completed, values, null);
    }

    /// <summary>
    /// Creates the result of a function that failed. The failure has already been logged and published when the engine
    /// returns one of these.
    /// </summary>
    /// <param name="error">The file, line, message and Lua traceback of the failure.</param>
    /// <returns>A <see cref="ScriptResultKind.Failed" /> result carrying <paramref name="error" />.</returns>
    public static ScriptResult Failed(ScriptErrorInfo error)
    {
        return new(ScriptResultKind.Failed, [], error);
    }
}
