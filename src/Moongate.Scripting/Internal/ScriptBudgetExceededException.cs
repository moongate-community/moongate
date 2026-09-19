using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>Thrown from the instruction hook when one resume runs past its budget. Typed so the scheduler counts it without parsing the message.</summary>
internal sealed class ScriptBudgetExceededException : LuaRuntimeException
{
    public long Instructions { get; }

    public ScriptBudgetExceededException(LuaState state, long instructions)
        : base(state, new LuaValue($"script budget exceeded: {instructions} instructions in one resume"), 1)
    {
        Instructions = instructions;
    }
}
