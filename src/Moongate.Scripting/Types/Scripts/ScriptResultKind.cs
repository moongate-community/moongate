namespace Moongate.Scripting.Types.Scripts;

/// <summary>How a call into Lua ended.</summary>
public enum ScriptResultKind
{
    /// <summary>The function ran to its end; its return values are in the result.</summary>
    Completed = 0,

    /// <summary>
    /// The function called <c>wait</c>. Its coroutine is parked on the timer wheel and will be resumed on the game loop;
    /// no values are returned to the caller.
    /// </summary>
    Suspended = 1,

    /// <summary>The function failed. The error is in the result, and has already been logged and published as a script error event.</summary>
    Failed = 2
}
