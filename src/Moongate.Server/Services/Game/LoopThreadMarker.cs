using Moongate.Core.Interfaces;

namespace Moongate.Server.Services.Game;

/// <summary>Captures and reports the game-loop thread id.</summary>
public sealed class LoopThreadMarker : ILoopThread
{
    private volatile int _loopThreadId = -1;

    public bool IsOnLoopThread
        => _loopThreadId == Environment.CurrentManagedThreadId;

    public void Capture()
        => _loopThreadId = Environment.CurrentManagedThreadId;
}
