using Lua;
using Lua.Runtime;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Runs Lua functions as coroutines and parks a coroutine that yields ("wait", seconds) on the timer
/// wheel. There is no run queue: the wheel's callback, already on the loop thread, resumes the coroutine.
/// </summary>
internal sealed class CoroutineScheduler
{
    private const string WaitTag = "wait";

    private readonly LuaState _state;
    private readonly ITimerService _timers;
    private readonly InstructionBudget _budget;
    private readonly ScriptOwnership _ownership;
    private readonly Action<ScriptErrorInfo> _onError;
    private readonly Dictionary<Guid, ScheduledCoroutine> _active = new();
    private readonly LuaStack _stack = new(32);

    public int ActiveCount => _active.Count;
    public long Resumed { get; private set; }
    public long Finished { get; private set; }
    public long Errors { get; private set; }
    public long BudgetAborts { get; private set; }

    public CoroutineScheduler(
        LuaState state,
        ITimerService timers,
        InstructionBudget budget,
        ScriptOwnership ownership,
        Action<ScriptErrorInfo> onError
    )
    {
        _state = state;
        _timers = timers;
        _budget = budget;
        _ownership = ownership;
        _onError = onError;
    }

    public CoroutineOutcome Start(LuaFunction function, string owner, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        // Unprotected on purpose: an error then surfaces as LuaRuntimeException with file, line and
        // traceback, which the catch below turns into ScriptErrorInfo. Protected mode strips the position.
        var coroutine = _state.CreateCoroutine(function, isProtectedMode: false);
        _budget.Install(coroutine);
        var entry = new ScheduledCoroutine(Guid.NewGuid(), coroutine, owner);
        _active[entry.Id] = entry;
        _ownership.TrackCoroutine(owner, entry.Id);
        _stack.Clear();

        foreach (var arg in args)
        {
            _stack.Push(LuaValueConverter.ToLua(arg, arg?.GetType() ?? typeof(object)));
        }

        return Resume(entry);
    }

    /// <summary>Cancels every pending coroutine and timer the owner file created. Running code is never interrupted; only future resumes are dropped.</summary>
    public void CancelOwned(string owner)
    {
        foreach (var timerId in _ownership.ReleaseTimers(owner))
        {
            _timers.UnregisterTimer(timerId);
        }

        foreach (var id in _ownership.ReleaseCoroutines(owner))
        {
            if (_active.Remove(id, out var entry) && entry.PendingTimer is not null)
            {
                _timers.UnregisterTimer(entry.PendingTimer);
            }
        }
    }

    private CoroutineOutcome Resume(ScheduledCoroutine entry)
    {
        Resumed++;
        _budget.BeginResume();
        int count;

        try
        {
            count = SyncValueTask.Run(entry.Coroutine.ResumeAsync(_stack, default));
        }
        catch (Exception exception)
        {
            return Fail(entry, ScriptErrorParser.FromException(exception, entry.Owner));
        }

        var values = _stack.AsSpan()[..count];

        if (values.Length == 0 || !values[0].Read<bool>())
        {
            var message = values.Length > 1 ? values[1].ToString() : "coroutine failed without a message";

            return Fail(entry, ScriptErrorParser.Parse(message, null) is var parsed && parsed.File.Length == 0
                ? parsed with { File = entry.Owner }
                : parsed);
        }

        if (entry.Coroutine.GetStatus() == LuaThreadStatus.Dead)
        {
            Forget(entry);
            Finished++;

            return new CoroutineOutcome(CoroutineOutcomeKind.Completed, ToClr(values[1..]), null);
        }

        if (values.Length >= 3 && values[1].Type == LuaValueType.String && values[1].Read<string>() == WaitTag &&
            values[2].Type == LuaValueType.Number)
        {
            var seconds = values[2].Read<double>();
            Park(entry, seconds);

            return CoroutineOutcome.Suspended;
        }

        return Fail(entry, new ScriptErrorInfo(entry.Owner, 0,
            "unsupported yield: coroutines may only yield through wait(seconds)", null));
    }

    private void Park(ScheduledCoroutine entry, double seconds)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(seconds, 0));
        string? timerId = null;
        timerId = _timers.RegisterTimer("lua-wait:" + entry.Owner, interval, () =>
        {
            if (timerId is not null)
            {
                _ownership.ForgetTimer(timerId);
            }

            entry.PendingTimer = null;

            if (!_active.ContainsKey(entry.Id))
            {
                return;
            }

            _stack.Clear();
            _stack.Push(new LuaValue(seconds));
            Resume(entry);
        });
        entry.PendingTimer = timerId;
        _ownership.TrackTimer(entry.Owner, timerId);
    }

    private CoroutineOutcome Fail(ScheduledCoroutine entry, ScriptErrorInfo error)
    {
        Forget(entry);
        Errors++;

        if (InstructionBudget.IsBudgetError(error.Message))
        {
            BudgetAborts++;
        }

        _onError(error);

        return new CoroutineOutcome(CoroutineOutcomeKind.Failed, [], error);
    }

    private void Forget(ScheduledCoroutine entry)
    {
        _active.Remove(entry.Id);
        _ownership.ForgetCoroutine(entry.Id);
    }

    private static object?[] ToClr(ReadOnlySpan<LuaValue> values)
    {
        var result = new object?[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            result[i] = LuaValueConverter.FromLua(values[i], typeof(object));
        }

        return result;
    }
}
