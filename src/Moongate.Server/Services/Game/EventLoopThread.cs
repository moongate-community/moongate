using Moongate.Core.Interfaces;
using SquidStd.Core.Interfaces.Threading;

namespace Moongate.Server.Services.Game;

/// <summary>Adapts the SquidStd event loop's own thread identity to <see cref="ILoopThread" />.</summary>
public sealed class EventLoopThread : ILoopThread
{
    private readonly IEventLoopService _loop;

    public bool IsOnLoopThread => _loop.IsOnLoopThread;

    public EventLoopThread(IEventLoopService loop)
    {
        _loop = loop;
    }
}
