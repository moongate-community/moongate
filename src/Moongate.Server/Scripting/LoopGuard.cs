using Moongate.Core.Interfaces;
using Serilog;

namespace Moongate.Server.Scripting;

/// <summary>
/// Diagnostic guard: warns when a world-mutating script operation runs off the game-loop thread,
/// making an otherwise-silent single-writer violation visible in the logs.
/// </summary>
public static class LoopGuard
{
    public static void Warn(ILoopThread loopThread, string operation, ILogger? logger = null)
    {
        if (!loopThread.IsOnLoopThread)
        {
            // Default to the ambient Serilog logger, resolved at call time so it honours the current
            // Log.Logger configuration; tests pass their own logger to avoid mutating global state.
            (logger ?? Log.ForContext(typeof(LoopGuard)))
                .Warning("{Operation} called off the game-loop thread; world mutation must run on the loop", operation);
        }
    }
}
