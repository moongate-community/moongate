using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Counts VM instructions through the runtime's hook and aborts a unit of execution that runs past the
/// budget. A unit is either one coroutine resume (<see cref="Resume{T}"/>) or one top-level chunk
/// (<see cref="Chunk{T}"/>); each gets a fresh counter, its own limit and its own cancellation source,
/// and restores the enclosing unit's afterwards.
/// </summary>
/// <remarks>
/// The hook never throws. In LuaCSharp 0.5.6 a hook that throws leaves the VM's in-hook flag set, so the
/// hook stops firing for the rest of that state's life, and <c>pcall</c> swallows the exception anyway.
/// The hook therefore records the abort and cancels the unit's token, which the VM checks per
/// instruction; the resulting <c>LuaCanceledException</c> cannot be swallowed by <c>pcall</c>, and the
/// scoping method turns it back into the recorded <see cref="ScriptBudgetExceededException"/>.
/// Deterministic by construction: the instruction count, not the clock, decides when the token is
/// cancelled, so the same script aborts at the same instruction on every machine.
/// </remarks>
internal sealed class InstructionBudget
{
    private readonly LuaState _state;
    private readonly int _maxInstructionsPerResume;
    private readonly int _maxInstructionsPerChunk;
    private readonly int _hookInterval;
    private long _instructionsThisUnit;
    private int _limit;
    private CancellationTokenSource? _unit;
    private ScriptBudgetExceededException? _abort;

    public InstructionBudget(LuaState state, int maxInstructionsPerResume, int maxInstructionsPerChunk, int hookInterval)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInstructionsPerResume);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInstructionsPerChunk);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hookInterval);
        _state = state;
        _maxInstructionsPerResume = maxInstructionsPerResume;
        _maxInstructionsPerChunk = maxInstructionsPerChunk;
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

            // Outside any unit the hook only counts; there is nothing to cancel. The abort is built here
            // because the state's position is only readable while the hook frame is on the stack.
            if (_unit is not null && _abort is null && _instructionsThisUnit > _limit)
            {
                _abort = new ScriptBudgetExceededException(context.State, _instructionsThisUnit);
                _unit.Cancel();
            }

            return new ValueTask<int>(context.Return());
        }), "", _hookInterval);
    }

    /// <summary>Runs one coroutine resume, or one host call, under the per-resume limit.</summary>
    /// <exception cref="ScriptBudgetExceededException">The unit ran past its limit.</exception>
    public T Resume<T>(Func<CancellationToken, T> unit)
    {
        return Run(unit, _maxInstructionsPerResume);
    }

    /// <summary>Runs one top-level chunk (the prelude, the bootstrap file, a file loaded by LoadFile) under the per-chunk limit.</summary>
    /// <exception cref="ScriptBudgetExceededException">The unit ran past its limit.</exception>
    public T Chunk<T>(Func<CancellationToken, T> unit)
    {
        return Run(unit, _maxInstructionsPerChunk);
    }

    private T Run<T>(Func<CancellationToken, T> unit, int limit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var enclosingInstructions = _instructionsThisUnit;
        var enclosingLimit = _limit;
        var enclosingUnit = _unit;
        var enclosingAbort = _abort;
        var source = new CancellationTokenSource();
        _instructionsThisUnit = 0;
        _limit = limit;
        _unit = source;
        _abort = null;

        try
        {
            T result;

            try
            {
                result = unit(source.Token);
            }
            catch (OperationCanceledException) when (_abort is not null)
            {
                // The runtime's cancellation exception says nothing useful; the recorded abort carries
                // the instruction count and the Lua position, and every catch site already knows it.
                throw _abort;
            }

            // A unit that returns normally after the hook tripped still ran past its budget.
            return _abort is null ? result : throw _abort;
        }
        finally
        {
            _instructionsThisUnit = enclosingInstructions;
            _limit = enclosingLimit;
            _unit = enclosingUnit;
            _abort = enclosingAbort;
            source.Dispose();
        }
    }
}
