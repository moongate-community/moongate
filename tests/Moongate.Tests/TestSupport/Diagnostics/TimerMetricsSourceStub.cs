using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class TimerMetricsSourceStub : ITimerService
{
    public TimerMetricsSnapshot Snapshot { get; set; }
    public int SnapshotReadCount { get; private set; }

    public TimerMetricsSourceStub(TimerMetricsSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public TimerMetricsSnapshot GetMetricsSnapshot()
    {
        SnapshotReadCount++;

        return Snapshot;
    }

    public string RegisterTimer(
        string name,
        TimeSpan interval,
        Action callback,
        TimeSpan? delay = null,
        bool repeat = false
    )
        => throw new NotSupportedException();

    public Task StartAsync()
        => throw new NotSupportedException();

    public Task StopAsync()
        => throw new NotSupportedException();

    public void UnregisterAllTimers()
        => throw new NotSupportedException();

    public bool UnregisterTimer(string timerId)
        => throw new NotSupportedException();

    public int UnregisterTimersByName(string name)
        => throw new NotSupportedException();
}
