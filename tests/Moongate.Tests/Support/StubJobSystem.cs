using SquidStd.Core.Interfaces.Jobs;

namespace Moongate.Tests.Support;

/// <summary>
/// An <see cref="IJobSystem" /> that runs work inline on the calling thread, so a test can assert what
/// the job did without waiting on a worker pool.
/// </summary>
public sealed class StubJobSystem : IJobSystem
{
    /// <summary>How many pieces of work were handed to the job system.</summary>
    public int Scheduled { get; private set; }

    public int WorkerCount => 1;

    public int PendingCount => 0;

    public int ActiveCount => 0;

    public long CompletedCount => Scheduled;

    public Task ScheduleAsync(Action work, CancellationToken cancellationToken = default)
    {
        Scheduled++;
        work();

        return Task.CompletedTask;
    }

    public Task<T> ScheduleAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        Scheduled++;

        return Task.FromResult(work());
    }

    public void Dispose() { }
}
