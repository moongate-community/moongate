using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Sessions;

namespace Moongate.Server.Core.Packets.Internal;

internal sealed class PacketContextWorkItem : IGameLoopWorkItem
{
    private readonly GameSession _originalSession;
    private readonly ISessionService _sessions;
    private readonly Action<GameSession> _action;
    private readonly CancellationToken _cancellationToken;
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<bool> Completion => _completion.Task;

    public PacketContextWorkItem(
        GameSession originalSession,
        ISessionService sessions,
        Action<GameSession> action,
        CancellationToken cancellationToken
    )
    {
        _originalSession = originalSession;
        _sessions = sessions;
        _action = action;
        _cancellationToken = cancellationToken;
    }

    public void Execute()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _completion.TrySetCanceled(_cancellationToken);

            return;
        }

        if (!_sessions.TryGet(_originalSession.SessionId, out var session) ||
            !ReferenceEquals(session, _originalSession) ||
            session.NetworkSession.State == NetworkSessionState.Disconnected ||
            session.NetworkSession.Client is not { IsConnected: true })
        {
            _completion.TrySetResult(false);

            return;
        }

        try
        {
            _action(session);
            _completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }
}
