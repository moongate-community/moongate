using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Counts VM instructions through the runtime's hook and aborts a unit of execution that runs past the
/// budget. A unit is one coroutine resume or one top-level chunk; each runs inside <see cref="Scoped{T}"/>,
/// which gives it a fresh counter and restores the enclosing one afterwards. Deterministic by
/// construction: the same script aborts at the same instruction on every machine.
/// </summary>
internal sealed class InstructionBudget
{
    private readonly LuaState _state;
    private readonly int _maxInstructionsPerUnit;
    private readonly int _hookInterval;
    private long _instructionsThisUnit;

    public InstructionBudget(LuaState state, int maxInstructionsPerResume, int hookInterval)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInstructionsPerResume);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hookInterval);
        _state = state;
        _maxInstructionsPerUnit = maxInstructionsPerResume;
        _hookInterval = hookInterval;
    }

    /// <summary>Installs the hook on the main state.</summary>
    public void Install()
    {
        Install(_state);
    }

    /// <summary>Installs the hook on a coroutine, which has its own hook slot.</summary>
    public void Install(LuaState coroutine)
    {
        coroutine.SetHook(new LuaFunction("moongate.budget", (context, _) =>
        {
            _instructionsThisUnit += _hookInterval;

            if (_instructionsThisUnit > _maxInstructionsPerUnit)
            {
                throw new ScriptBudgetExceededException(context.State, _instructionsThisUnit);
            }

            return new ValueTask<int>(context.Return());
        }), "", _hookInterval);
    }

    /// <summary>Runs one unit of Lua execution with a fresh counter, restoring the enclosing unit's count afterwards.</summary>
    public T Scoped<T>(Func<T> unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var enclosing = _instructionsThisUnit;
        _instructionsThisUnit = 0;

        try
        {
            return unit();
        }
        finally
        {
            _instructionsThisUnit = enclosing;
        }
    }
}
