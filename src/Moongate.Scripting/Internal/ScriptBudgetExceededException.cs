using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>
///     Built inside the instruction hook when one unit of execution — a coroutine resume or a top-level
///     chunk — runs past its budget, and thrown by the scoping method once the unit has stopped. Typed so
///     the scheduler counts it without parsing the message, and a <see cref="LuaRuntimeException" /> so every
///     existing catch site keeps working.
/// </summary>
internal sealed class ScriptBudgetExceededException : LuaRuntimeException
{
    public long Instructions { get; }

    public ScriptBudgetExceededException(LuaState state, long instructions)
        : base(state, new LuaValue($"script budget exceeded: {instructions} instructions in one unit"))
    {
        Instructions = instructions;
    }
}
