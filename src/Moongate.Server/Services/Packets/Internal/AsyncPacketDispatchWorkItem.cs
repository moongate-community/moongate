using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Sessions;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class AsyncPacketDispatchWorkItem : IGameLoopWorkItem
{
    private readonly ISessionService _sessions;
    private readonly AsyncPacketJob _job;
    private readonly AsyncPacketExecutor _executor;

    public AsyncPacketDispatchWorkItem(ISessionService sessions, AsyncPacketJob job, AsyncPacketExecutor executor)
    {
        _sessions = sessions;
        _job = job;
        _executor = executor;
    }

    public void Execute()
    {
        var original = _job.Session;

        if (!_sessions.TryGet(original.SessionId, out var current) ||
            !ReferenceEquals(current, original) ||
            current.NetworkSession.State == NetworkSessionState.Disconnected ||
            current.NetworkSession.Client is not { IsConnected: true } ||
            !_executor.TryEnqueue(_job))
        {
            _executor.Release(_job);
        }
    }
}
