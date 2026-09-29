using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;
using Serilog;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class SessionRetirementWorkItem : IGameLoopWorkItem
{
    private readonly ISessionService _sessions;
    private readonly long _sessionId;
    private readonly IReadOnlyList<ISessionClosedListener> _listeners;
    private readonly ILogger _logger = Log.ForContext<SessionRetirementWorkItem>();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completion => _completion.Task;

    public SessionRetirementWorkItem(
        ISessionService sessions,
        long sessionId,
        IReadOnlyList<ISessionClosedListener> listeners
    )
    {
        _sessions = sessions;
        _sessionId = sessionId;
        _listeners = listeners;
    }

    public void Execute()
    {
        try
        {
            if (_sessions.TryGet(_sessionId, out var session))
            {
                TellListeners(session);
                session.NetworkSession.DetachClient();
                _sessions.Remove(_sessionId);
            }

            _completion.TrySetResult();
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);

            throw;
        }
    }

    private void TellListeners(GameSession session)
    {
        foreach (var listener in _listeners)
        {
            try
            {
                listener.OnSessionClosed(session);
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    "{Listener} failed while session {SessionId} was closing",
                    listener.GetType().Name,
                    session.SessionId
                );
            }
        }
    }
}
