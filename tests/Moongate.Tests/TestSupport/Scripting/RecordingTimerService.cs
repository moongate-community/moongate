using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>An ITimerService that records registrations and fires them on demand, on the calling thread.</summary>
public sealed class RecordingTimerService : ITimerService
{
    private int _next;

    public List<RegisteredTimer> Timers { get; } = [];
    public List<string> Unregistered { get; } = [];

    /// <summary>When true, RegisterTimer throws instead of registering, as the real wheel does at capacity or once closed.</summary>
    public bool ThrowOnRegister { get; set; }

    public string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false)
    {
        if (ThrowOnRegister)
        {
            throw new InvalidOperationException("Timer capacity has been reached.");
        }

        var id = "t" + (++_next);
        Timers.Add(new RegisteredTimer(id, name, interval, delay, repeat, callback));

        return id;
    }

    public bool UnregisterTimer(string timerId)
    {
        Unregistered.Add(timerId);

        return Timers.RemoveAll(timer => timer.Id == timerId) > 0;
    }

    public int UnregisterTimersByName(string name)
    {
        return Timers.RemoveAll(timer => timer.Name == name);
    }

    public void UnregisterAllTimers()
    {
        Timers.Clear();
    }

    public TimerMetricsSnapshot GetMetricsSnapshot()
    {
        throw new NotSupportedException();
    }

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;

    /// <summary>Runs the callback of a registered timer; a one-shot timer is removed first, as the wheel would.</summary>
    public void Fire(string id)
    {
        var timer = Timers.Single(candidate => candidate.Id == id);

        if (!timer.Repeat)
        {
            Timers.Remove(timer);
        }

        timer.Callback();
    }
}
