using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets.Internal;
using Moongate.Server.Core.Types.Sessions;

namespace Moongate.Server.Core.Packets;

/// <summary>Provides session-scoped operations to an asynchronous packet handler.</summary>
public sealed class PacketContext
{
    private readonly GameSession _originalSession;
    private readonly IGameLoopService _gameLoop;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;

    public long SessionId => _originalSession.SessionId;

    public PacketContext(
        GameSession originalSession,
        IGameLoopService gameLoop,
        ISessionService sessions,
        IPacketSendService sender
    )
    {
        _originalSession = originalSession;
        _gameLoop = gameLoop;
        _sessions = sessions;
        _sender = sender;
    }

    /// <summary>Queues an outgoing packet only while the original session remains connected.</summary>
    public bool TrySend(IOutgoingPacket packet)
        => IsOriginalSessionConnected() && _sender.TrySend(SessionId, packet);

    /// <summary>Runs one state change on the game loop; false means the original session is gone.</summary>
    public async ValueTask<bool> RunOnGameLoopAsync(
        Action<GameSession> action,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_gameLoop.IsOnLoopThread)
        {
            throw new InvalidOperationException("An asynchronous packet handler cannot wait on the game loop thread.");
        }

        using var admissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var item = new PacketContextWorkItem(_originalSession, _sessions, action, cancellationToken);
        Task admission;

        try
        {
            admission = _gameLoop.PostAsync(item, admissionCancellation.Token).AsTask();
        }
        catch (InvalidOperationException) when (_gameLoop.Completion.IsCompleted)
        {
            return false;
        }

        if (await Task.WhenAny(admission, _gameLoop.Completion).ConfigureAwait(false) != admission)
        {
            await admissionCancellation.CancelAsync().ConfigureAwait(false);

            try
            {
                await admission.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
        else
        {
            await admission.ConfigureAwait(false);
        }

        var completion = item.Completion.WaitAsync(cancellationToken);

        if (await Task.WhenAny(completion, _gameLoop.Completion).ConfigureAwait(false) != completion &&
            !completion.IsCompleted)
        {
            return false;
        }

        return await completion.ConfigureAwait(false);
    }

    private bool IsOriginalSessionConnected()
        => _sessions.TryGet(SessionId, out var session) &&
           ReferenceEquals(session, _originalSession) &&
           session.NetworkSession.State != NetworkSessionState.Disconnected &&
           session.NetworkSession.Client is { IsConnected: true };
}
