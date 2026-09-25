using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Tests.Support.GameLoop;

public sealed class BlockingGameLoopWorkItem : IGameLoopWorkItem, IDisposable
{
    private readonly ManualResetEventSlim _release = new();
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Execute()
    {
        _entered.TrySetResult();

        if (!_release.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The test did not release the blocking handler.");
        }
    }

    public void Release()
    {
        _release.Set();
    }

    public void Dispose()
    {
        // The loop may still be returning from Wait; do not dispose its gate concurrently.
        _release.Set();
    }
}
