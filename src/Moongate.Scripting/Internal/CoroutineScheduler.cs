using Lua;
using Lua.Runtime;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces.Internal;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Runs Lua functions as coroutines and parks a coroutine that yields ("wait", seconds) on the timer
/// wheel. There is no run queue: the wheel's callback, already on the loop thread, resumes the coroutine.
/// Nothing thrown by Lua leaves this class; every failure becomes a ScriptResult and an onError call.
/// </summary>
internal sealed class CoroutineScheduler : IScriptScheduler
{
    private const string WaitTag = "wait";
    private static readonly double MaxWaitSeconds = TimeSpan.MaxValue.TotalSeconds;

    private readonly LuaState _state;
    private readonly ITimerService _timers;
    private readonly InstructionBudget _budget;
    private readonly ScriptOwnership _ownership;
    private readonly Action<ScriptErrorInfo> _onError;
    private readonly Func<string?> _currentOwner;
    private readonly Dictionary<Guid, ScheduledCoroutine> _active = new();
    private readonly LuaStack _stack = new(32);
    private bool _resuming;
    private bool _stopped;
    private ScheduledCoroutine? _current;

    public int ActiveCount => _active.Count;
    public long Resumed { get; private set; }
    public long Finished { get; private set; }
    public long Errors { get; private set; }
    public long BudgetAborts { get; private set; }

    /// <summary>
    /// Gets the owner of the code executing right now: the running coroutine's owner during a resume, otherwise the file
    /// being loaded; null when neither applies.
    /// </summary>
    public string? CurrentOwner => _current?.Owner ?? _currentOwner();

    public CoroutineScheduler(
        LuaState state,
        ITimerService timers,
        InstructionBudget budget,
        ScriptOwnership ownership,
        Action<ScriptErrorInfo> onError,
        Func<string?> currentOwner
    )
    {
        _state = state;
        _timers = timers;
        _budget = budget;
        _ownership = ownership;
        _onError = onError;
        _currentOwner = currentOwner;
    }

    /// <summary>
    /// Cancels every pending timer and coroutine regardless of owner, and makes every later <see cref="Start" />
    /// fail instead of touching the Lua state. Called once, right before the engine disposes the state: a
    /// periodic timer callback that fires after that point must not reach <c>CreateCoroutine</c> on a disposed
    /// state.
    /// </summary>
    public void CancelAll()
    {
        foreach (var timerId in _ownership.ReleaseAllTimers())
        {
            _timers.UnregisterTimer(timerId);
        }

        foreach (var entry in _active.Values)
        {
            if (entry.PendingTimer is not null)
            {
                _timers.UnregisterTimer(entry.PendingTimer);
            }

            entry.Dispose();
        }

        _active.Clear();
        _ownership.Clear();
        _stopped = true;
    }

    /// <summary>
    /// Cancels every pending coroutine and timer the owner file created. Running code is never interrupted; only future
    /// resumes are dropped.
    /// </summary>
    public void CancelOwned(string owner)
    {
        foreach (var timerId in _ownership.ReleaseTimers(owner))
        {
            _timers.UnregisterTimer(timerId);
        }

        foreach (var id in _ownership.ReleaseCoroutines(owner))
        {
            if (!_active.Remove(id, out var entry))
            {
                continue;
            }

            if (entry.PendingTimer is not null)
            {
                _timers.UnregisterTimer(entry.PendingTimer);
            }

            entry.Dispose();
        }
    }

    /// <summary>
    /// Starts <paramref name="function" /> as a coroutine owned by <paramref name="owner" /> and runs it until it returns,
    /// waits, or fails.
    /// </summary>
    /// <exception cref="InvalidCastException">An argument has no Lua representation; nothing is registered.</exception>
    /// <exception cref="InvalidOperationException">Called while another resume is running on this scheduler.</exception>
    public ScriptResult Start(LuaFunction function, string owner, params object?[] args)
    {
        if (_stopped)
        {
            return ScriptResult.Failed(new(owner, 0, "the script engine has stopped", null));
        }

        ArgumentNullException.ThrowIfNull(function);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        EnsureNotResuming();

        // Convert before registering, so an unconvertible argument cannot leave a phantom coroutine behind.
        var arguments = new LuaValue[args.Length];

        for (var i = 0; i < args.Length; i++)
        {
            arguments[i] = LuaValueConverter.ToLua(args[i], args[i]?.GetType() ?? typeof(object));
        }

        var coroutine = _state.CreateCoroutine(function);
        _budget.Install(coroutine);
        var entry = new ScheduledCoroutine(Guid.NewGuid(), coroutine, owner);
        _active[entry.Id] = entry;
        _ownership.TrackCoroutine(owner, entry.Id);
        _stack.Clear();
        _stack.PushRange(arguments);

        return Resume(entry);
    }

    private void EnsureNotResuming()
    {
        if (_resuming)
        {
            throw new InvalidOperationException(
                "Coroutine resumes cannot nest: start or resume a coroutine from a timer callback, not from inside running Lua."
            );
        }
    }

    private ScriptResult Fail(ScheduledCoroutine entry, ScriptErrorInfo error)
    {
        if (entry.PendingTimer is not null)
        {
            _timers.UnregisterTimer(entry.PendingTimer);
            _ownership.ForgetTimer(entry.PendingTimer);
            entry.PendingTimer = null;
        }

        Forget(entry);
        Errors++;
        _onError(error);

        return ScriptResult.Failed(error);
    }

    /// <summary>Drops a finished or failed coroutine and releases its budget source; the entry is never resumed again.</summary>
    private void Forget(ScheduledCoroutine entry)
    {
        _active.Remove(entry.Id);
        _ownership.ForgetCoroutine(entry.Id);
        entry.Dispose();
    }

    private ScriptResult Park(ScheduledCoroutine entry, double seconds)
    {
        string? timerId = null;

        try
        {
            timerId = _timers.RegisterTimer(
                "lua-wait:" + entry.Owner,
                TimeSpan.FromSeconds(seconds),
                () =>
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
                    _stack.Push(new(seconds));
                    Resume(entry);
                }
            );
        }
        catch (Exception exception)
        {
            // The wheel refuses a registration at capacity, and it closes on a callback that throws.
            // A 'wait' that cannot be scheduled fails its own coroutine rather than the caller: this
            // runs inside a timer callback whenever the coroutine was resumed by one.
            return Fail(
                entry,
                new(
                    entry.Owner,
                    0,
                    $"'wait' could not schedule the timer: {exception.Message}",
                    null
                )
            );
        }

        entry.PendingTimer = timerId;
        _ownership.TrackTimer(entry.Owner, timerId);

        return ScriptResult.Suspended;
    }

    private ScriptResult Resume(ScheduledCoroutine entry)
    {
        EnsureNotResuming();

        if (_stopped)
        {
            return ScriptResult.Failed(new(entry.Owner, 0, "the script engine has stopped", null));
        }

        _resuming = true;
        _current = entry;
        Resumed++;

        try
        {
            int count;

            try
            {
                // The coroutine's own source, not a per-resume one: the runtime keeps the token of the
                // first resume with the suspended frames and checks that one on every later resume.
                count = _budget.Resume(entry.Budget, token => SyncValueTask.Run(entry.Coroutine.ResumeAsync(_stack, token)));
            }
            catch (ScriptBudgetExceededException exception)
            {
                BudgetAborts++;

                return Fail(entry, ScriptErrorParser.FromException(exception, entry.Owner));
            }
            catch (Exception exception)
            {
                return Fail(entry, ScriptErrorParser.FromException(exception, entry.Owner));
            }

            var values = _stack.AsSpan()[..count];

            if (values.Length == 0 || !values[0].Read<bool>())
            {
                var message = values.Length > 1 ? values[1].ToString() : "coroutine failed without a message";

                return Fail(entry, WithOwner(ScriptErrorParser.Parse(message, null), entry.Owner));
            }

            if (entry.Coroutine.GetStatus() == LuaThreadStatus.Dead)
            {
                Forget(entry);
                Finished++;

                return ScriptResult.Completed(ToClr(values[1..]));
            }

            if (values.Length >= 3 &&
                values[1].Type == LuaValueType.String &&
                values[1].Read<string>() == WaitTag &&
                values[2].Type == LuaValueType.Number)
            {
                var seconds = values[2].Read<double>();

                if (!double.IsFinite(seconds) || seconds <= 0 || seconds > MaxWaitSeconds)
                {
                    return Fail(
                        entry,
                        new(
                            entry.Owner,
                            0,
                            $"wait(seconds) needs a positive number of seconds, got {seconds}",
                            null
                        )
                    );
                }

                return Park(entry, seconds);
            }

            return Fail(
                entry,
                new(
                    entry.Owner,
                    0,
                    "unsupported yield: coroutines may only yield through wait(seconds)",
                    null
                )
            );
        }
        finally
        {
            _resuming = false;
            _current = null;
        }
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

    private static ScriptErrorInfo WithOwner(ScriptErrorInfo error, string owner)
        => error.File.Length == 0 ? error with { File = owner } : error;
}
