using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Services.Persistence.Internal;

/// <summary>
///     Reports capture failures to the save worker without faulting the game loop.
/// </summary>
internal sealed class WorldSaveCaptureWorkItem : IGameLoopWorkItem
{
    private readonly Action _capture;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completion => _completion.Task;

    public WorldSaveCaptureWorkItem(Action capture)
    {
        _capture = capture;
    }

    public void Execute()
    {
        try
        {
            _capture();
            _completion.TrySetResult();
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }
}
