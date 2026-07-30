using Moongate.Core.Interfaces;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Core.Interfaces.Timing;

namespace Moongate.Tests.Support;

/// <summary>
/// A game loop that runs posted work inline, so a test can assert the contract — the work happens on the
/// loop and the caller waits for it — with no timing to flake on. Built with <c>answers: false</c> it
/// counts the post and swallows the work, standing in for a loop that has stalled.
/// </summary>
public sealed class StubGameLoopContext : IGameLoopContext
{
    private readonly bool _answers;

    public StubGameLoopContext(bool answers = true)
    {
        _answers = answers;
    }

    /// <summary>How many times work was handed to the loop.</summary>
    public int PostCount { get; private set; }

    /// <summary>Repeating timers registered on this loop, keyed by name, holding the callback handed over.</summary>
    public Dictionary<string, Action> Repeating { get; } = [];

    /// <summary>The interval the last repeating timer was registered with.</summary>
    public TimeSpan RepeatingInterval { get; private set; }

    /// <summary>The initial delay the last repeating timer was registered with, if any.</summary>
    public TimeSpan? RepeatingDelay { get; private set; }

    public IMainThreadDispatcher Dispatcher => throw new NotSupportedException();

    public ITimerService Timers => throw new NotSupportedException();

    public bool Cancel(string timerId)
        => Repeating.Remove(timerId);

    public Task<T> InvokeAsync<T>(Func<T> work, TimeSpan? timeout = null)
    {
        PostCount++;

        if (_answers)
        {
            return Task.FromResult(work());
        }

        return Task.FromException<T>(new TimeoutException("Stub game loop is not answering."));
    }

    public void Post(Action action)
    {
        if (_answers)
        {
            action();
        }

        // Counted after the work, not before: the caller may be a thread-pool continuation, and a
        // test waiting on PostCount would otherwise be released between the two and assert on
        // effects the action had not produced yet.
        PostCount++;
    }

    public string Schedule(string name, TimeSpan delay, Action callback)
        => throw new NotSupportedException();

    public string ScheduleRepeating(string name, TimeSpan interval, Action callback, TimeSpan? delay = null)
    {
        Repeating[name] = callback;
        RepeatingInterval = interval;
        RepeatingDelay = delay;

        return name;
    }
}
