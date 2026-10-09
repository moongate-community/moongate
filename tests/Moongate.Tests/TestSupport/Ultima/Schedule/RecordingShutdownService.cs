using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Ultima.Schedule;

public sealed class RecordingShutdownService : IServerShutdownService
{
    private readonly TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Requested => _requested.Task;

    public int Requests { get; private set; }

    public void RequestShutdown()
    {
        Requests++;
        _requested.TrySetResult();
    }

    /// <summary>
    ///     Marks a shutdown as asked by someone else, such as the shutdown command.
    /// </summary>
    public void MarkRequested()
    {
        _requested.TrySetResult();
    }
}
