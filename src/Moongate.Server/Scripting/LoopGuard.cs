using Moongate.Core.Interfaces;
using Serilog;

namespace Moongate.Server.Scripting;

/// <summary>
/// Diagnostic guard: warns when a world-mutating script operation runs off the game-loop thread,
/// making an otherwise-silent single-writer violation visible in the logs.
/// </summary>
public static class LoopGuard
{
    private static readonly ILogger Logger = Log.ForContext(typeof(LoopGuard));

    public static void Warn(ILoopThread loopThread, string operation)
    {
        if (!loopThread.IsOnLoopThread)
        {
            Logger.Warning("{Operation} called off the game-loop thread; world mutation must run on the loop", operation);
        }
    }
}
