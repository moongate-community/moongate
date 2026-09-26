using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Scripting.Internal;

/// <summary>
///     Runs one engine lifecycle step on the game loop thread and reports its outcome to the caller instead of faulting
///     the loop.
/// </summary>
internal sealed class ScriptLifecycleWorkItem : IGameLoopWorkItem
{
    private readonly Action _step;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Completes when the step has run, faulted with the step's exception when it threw.
    /// </summary>
    public Task Completion => _completion.Task;

    public ScriptLifecycleWorkItem(Action step)
    {
        _step = step;
    }

    /// <inheritdoc />
    public void Execute()
    {
        try
        {
            _step();
            _completion.TrySetResult();
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }
}
