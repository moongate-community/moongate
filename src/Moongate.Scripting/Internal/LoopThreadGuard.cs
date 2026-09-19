using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Scripting.Internal;

internal sealed class LoopThreadGuard : IScriptThreadGuard
{
    private readonly IGameLoopService _gameLoop;

    public LoopThreadGuard(IGameLoopService gameLoop)
    {
        _gameLoop = gameLoop;
    }

    public void EnsureScriptThread(string member)
    {
        if (!_gameLoop.IsOnLoopThread)
        {
            throw new InvalidOperationException(
                $"{member} must be called on the game loop thread. Post a work item to the loop instead.");
        }
    }
}
