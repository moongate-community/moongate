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

    public IMainThreadDispatcher Dispatcher => throw new NotSupportedException();

    public ITimerService Timers => throw new NotSupportedException();

    public void Post(Action action)
    {
        PostCount++;

        if (_answers)
        {
            action();
        }
    }

    public bool Cancel(string timerId)
        => throw new NotSupportedException();

    public string Schedule(string name, TimeSpan delay, Action callback)
        => throw new NotSupportedException();

    public string ScheduleRepeating(string name, TimeSpan interval, Action callback, TimeSpan? delay = null)
        => throw new NotSupportedException();
}
