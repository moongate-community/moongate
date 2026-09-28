using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;

namespace Moongate.Tests.TestSupport.Commands;

public sealed class DelayedCommandExecutor : ICommandExecutor
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public async Task ExecuteAsync(CommandContext context)
    {
        _started.TrySetResult();
        await _release.Task;
        context.Print("done");
    }

    public void Release()
    {
        _release.TrySetResult();
    }
}
