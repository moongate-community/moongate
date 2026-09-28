using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Runs one step on the game loop and completes when it has run, with its exception if it threw.
/// </summary>
public sealed class LoopActionWorkItem : IGameLoopWorkItem
{
    private readonly Action _action;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completion => _completion.Task;

    public LoopActionWorkItem(Action action)
    {
        _action = action;
    }

    public void Execute()
    {
        try
        {
            _action();
            _completion.TrySetResult();
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }
}
