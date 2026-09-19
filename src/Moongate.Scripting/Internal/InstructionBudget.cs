using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Counts VM instructions through the runtime's hook and aborts a resume that runs past the budget.
/// Deterministic by construction: the same script aborts at the same instruction on every machine.
/// </summary>
internal sealed class InstructionBudget
{
    private readonly LuaState _state;
    private readonly int _maxInstructionsPerResume;
    private readonly int _hookInterval;
    private long _instructionsThisResume;

    public long LastResumeInstructions => _instructionsThisResume;

    public InstructionBudget(LuaState state, int maxInstructionsPerResume, int hookInterval)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInstructionsPerResume);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hookInterval);
        _state = state;
        _maxInstructionsPerResume = maxInstructionsPerResume;
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
            _instructionsThisResume += _hookInterval;

            if (_instructionsThisResume > _maxInstructionsPerResume)
            {
                throw new LuaRuntimeException(
                    context.State,
                    new LuaValue($"script budget exceeded: {_instructionsThisResume} instructions in one resume"),
                    1
                );
            }

            return new ValueTask<int>(context.Return());
        }), "", _hookInterval);
    }

    /// <summary>Resets the counter. Call before every resume and every top-level chunk execution.</summary>
    public void BeginResume()
    {
        _instructionsThisResume = 0;
    }

    public static bool IsBudgetError(string message)
    {
        return message.Contains("script budget exceeded", StringComparison.Ordinal);
    }
}
