using Moongate.Core.Interfaces;
using Moongate.Server.Abstractions.Data.Config;
using Serilog;

namespace Moongate.Server.Services.Game;

/// <summary>
/// Default <see cref="ILoopAffinity" />: reads the strict flag from <see cref="MoongateConfig" /> and
/// checks <see cref="ILoopThread" />. Strict throws off the loop; non-strict warns and lets the shard
/// keep running.
/// </summary>
public sealed class LoopAffinity : ILoopAffinity
{
    private readonly ILoopThread _loopThread;
    private readonly bool _strict;
    private readonly ILogger _logger;

    public LoopAffinity(ILoopThread loopThread, MoongateConfig config, ILogger? logger = null)
    {
        _loopThread = loopThread;
        _strict = config.StrictLoopAffinity;
        _logger = logger ?? Log.ForContext<LoopAffinity>();
    }

    public void AssertOnLoop(string operation)
    {
        if (_loopThread.IsOnLoopThread)
        {
            return;
        }

        if (_strict)
        {
            throw new InvalidOperationException(
                $"{operation} called off the game-loop thread; world mutation must run on the loop."
            );
        }

        _logger.Warning(
            "{Operation} called off the game-loop thread; world mutation must run on the loop",
            operation
        );
    }
}
