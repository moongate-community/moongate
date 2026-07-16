using MoonSharp.Interpreter;
using Moongate.Core.Interfaces;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Scripting.Lua.Interfaces.Events;

namespace Moongate.Scripting;

/// <summary>
/// Runs Lua event callbacks on the game-loop thread: inline when already on the loop, otherwise
/// posted onto the main-thread dispatcher (returning nil, since off-loop handlers are fire-and-forget).
/// </summary>
public sealed class LoopAffineInvokeMarshaller : ILuaInvokeMarshaller
{
    private readonly ILoopThread _loopThread;
    private readonly IMainThreadDispatcher _dispatcher;

    public LoopAffineInvokeMarshaller(ILoopThread loopThread, IMainThreadDispatcher dispatcher)
    {
        _loopThread = loopThread;
        _dispatcher = dispatcher;
    }

    public DynValue Invoke(Func<DynValue> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (_loopThread.IsOnLoopThread)
        {
            return call();
        }

        _dispatcher.Post(() => call());

        return DynValue.Nil;
    }
}
