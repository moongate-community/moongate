using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Tests.Support.GameLoop;

public sealed class ActionGameLoopWorkItem : IGameLoopWorkItem
{
    private readonly Action _action;

    public ActionGameLoopWorkItem(Action action)
    {
        _action = action;
    }

    public void Execute()
        => _action();
}
