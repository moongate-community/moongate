using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class SessionRetirementWorkItem : IGameLoopWorkItem
{
    private readonly ISessionService _sessions;
    private readonly long _sessionId;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completion => _completion.Task;

    public SessionRetirementWorkItem(ISessionService sessions, long sessionId)
    {
        _sessions = sessions;
        _sessionId = sessionId;
    }

    public void Execute()
    {
        try
        {
            if (_sessions.TryGet(_sessionId, out var session))
            {
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
}
