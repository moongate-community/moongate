using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Ultima.Commands;

public sealed class ControlledWorldSaveService : IWorldSaveService
{
    public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }

    public void Activate()
    {
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Calls++;

        return Completion.Task.WaitAsync(cancellationToken);
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(bool saveFinal)
    {
        return Task.CompletedTask;
    }
}
