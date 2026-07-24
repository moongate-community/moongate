using Moongate.Core.Interfaces;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Core.Interfaces.Timing;

namespace Moongate.Server.Services.Game;

/// <summary>
/// Server-side <see cref="IGameLoopContext" />: bundles the SquidStd main-thread dispatcher and
/// timer service so gameplay services take one game-loop handle instead of depending on both.
/// </summary>
public sealed class GameLoopContext : IGameLoopContext
{
    public IMainThreadDispatcher Dispatcher { get; }

    public ITimerService Timers { get; }

    public GameLoopContext(IMainThreadDispatcher dispatcher, ITimerService timers)
    {
        Dispatcher = dispatcher;
        Timers = timers;
    }

    public bool Cancel(string timerId)
        => Timers.UnregisterTimer(timerId);

    public void Post(Action action)
        => Dispatcher.Post(action);

    public string Schedule(string name, TimeSpan delay, Action callback)
        => Timers.RegisterTimer(name, delay, callback, repeat: false);

    public string ScheduleRepeating(string name, TimeSpan interval, Action callback, TimeSpan? delay = null)
        => Timers.RegisterTimer(name, interval, callback, delay, true);

    public async Task<T> InvokeAsync<T>(Func<T> work, TimeSpan? timeout = null)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.Post(() =>
            {
                try
                {
                    completion.SetResult(work());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }
        );

        return await completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(5));
    }
}
