using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Ctlstrap;

public sealed class ShutdownWorldSaveService : IWorldSaveService
{
    public TaskCompletionSource Activated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource StopStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource SaveCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool? SaveFinal { get; private set; }

    public void Activate()
    {
        Activated.TrySetResult();
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return SaveCompleted.Task.WaitAsync(cancellationToken);
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
        SaveFinal = saveFinal;
        StopStarted.TrySetResult();

        return SaveCompleted.Task;
    }
}
