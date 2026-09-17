using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Tests.Support.Sessions;

internal sealed class SessionGameLoopWorkItem : IGameLoopWorkItem
{
    private readonly Action _action;
    private readonly TaskCompletionSource _completion;

    public SessionGameLoopWorkItem(Action action, TaskCompletionSource completion)
    {
        _action = action;
        _completion = completion;
    }

    public void Execute()
    {
        try
        {
            _action();
            _completion.SetResult();
        }
        catch (Exception exception)
        {
            _completion.SetException(exception);
        }
    }
}
