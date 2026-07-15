using Moongate.Core.Interfaces;
using Serilog;

namespace Moongate.Server.Scripting;

/// <summary>
/// Diagnostic guard: warns when a world-mutating script operation runs off the game-loop thread,
/// making an otherwise-silent single-writer violation visible in the logs.
/// </summary>
public static class LoopGuard
{
    public static void Warn(ILoopThread loopThread, string operation)
    {
        if (!loopThread.IsOnLoopThread)
        {
            // Resolve the logger at call time (not a cached static) so it honours the current
            // Log.Logger configuration; the off-loop path is rare, so the cost is negligible.
            Log.ForContext(typeof(LoopGuard))
                .Warning("{Operation} called off the game-loop thread; world mutation must run on the loop", operation);
        }
    }
}
