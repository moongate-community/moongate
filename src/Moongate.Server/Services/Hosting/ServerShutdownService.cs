using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Hosting;

/// <summary>
///     Carries a non-blocking shutdown request from commands to the host's run loop.
/// </summary>
public sealed class ServerShutdownService : IServerShutdownService
{
    private readonly TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public Task Requested => _requested.Task;

    /// <inheritdoc />
    public void RequestShutdown()
    {
        _requested.TrySetResult();
    }
}
