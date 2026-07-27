using Moongate.Core.Interfaces;
using MoonSharp.Interpreter;
using Serilog;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Scripting.Lua.Interfaces.Events;

namespace Moongate.Scripting;

/// <summary>
/// Runs Lua event callbacks on the game-loop thread: inline when already on the loop, otherwise
/// posted onto the main-thread dispatcher (returning nil, since off-loop handlers are
/// fire-and-forget). Warns when an off-loop call discards a non-nil result, so a silently dropped
/// return value is visible.
/// </summary>
public sealed class LoopAffineInvokeMarshaller : ILuaInvokeMarshaller
{
    private readonly ILoopThread _loopThread;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly ILogger _logger;

    public LoopAffineInvokeMarshaller(ILoopThread loopThread, IMainThreadDispatcher dispatcher, ILogger? logger = null)
    {
        _loopThread = loopThread;
        _dispatcher = dispatcher;
        _logger = logger ?? Log.ForContext<LoopAffineInvokeMarshaller>();
    }

    public DynValue Invoke(Func<DynValue> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (_loopThread.IsOnLoopThread)
        {
            return call();
        }

        _dispatcher.Post(() =>
            {
                var result = call();

                if (result is not null && !result.IsNil())
                {
                    _logger.Warning(
                        "Lua event callback ran off the game loop and its result was discarded (returned {ResultType})",
                        result.Type
                    );
                }
            }
        );

        return DynValue.Nil;
    }
}
