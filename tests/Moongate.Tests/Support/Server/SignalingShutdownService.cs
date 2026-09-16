using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.Support.Server;

public sealed class SignalingShutdownService : IMoongateStartupService
{
    private readonly TaskCompletionSource _shutdownStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ShutdownStarted => _shutdownStarted.Task;

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _shutdownStarted.TrySetResult();
        return Task.CompletedTask;
    }
}
