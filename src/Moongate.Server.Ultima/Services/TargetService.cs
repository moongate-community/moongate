using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Internal.Targeting;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the one target a player may have in its session, as ModernUO keeps one per mobile: a new one ends the old
///     as overridden, a server cancel or the session closing ends it too. Ids count up per session, as POL's.
/// </summary>
public sealed class TargetService : ITargetService
{
    private readonly ILogger _logger = Log.ForContext<TargetService>();
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;
    private readonly IGameLoopService _loop;

    public TargetService(IMobileService mobiles, IPacketSendService sender, IGameLoopService loop)
    {
        _mobiles = mobiles;
        _sender = sender;
        _loop = loop;
    }

    public void Begin(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        Action<GameSession, TargetResult> callback
    )
    {
        if (!session.CharacterId.IsValid || !_mobiles.IsInWorld(session.CharacterId))
        {
            Invoke(session, callback, TargetResult.Canceled(TargetCancelType.Canceled));

            return;
        }

        var state = State(session);
        var previous = state.Pending;
        state.LastId = state.LastId == int.MaxValue ? 1 : state.LastId + 1;
        state.Pending = new(state.LastId, cursor, callback);

        if (previous is not null)
        {
            Invoke(session, previous.Callback, TargetResult.Canceled(TargetCancelType.Overridden));
        }

        _sender.TrySend(session.SessionId, new TargetCursorPacket(cursor, state.LastId, flags));
    }

    public async Task<TargetResult> RequestAsync(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        CancellationToken cancellationToken = default
    )
    {
        if (_loop.IsOnLoopThread)
        {
            throw new InvalidOperationException("A target cannot be awaited on the game loop thread; use Begin.");
        }

        var completion = new TaskCompletionSource<TargetResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = 0;
        var begin = new LoopActionWorkItem(() =>
            {
                Begin(session, cursor, flags, (_, result) => completion.TrySetResult(result));
                id = session.Get(TargetSessionKeys.State)?.Pending?.Id ?? 0;
            }
        );
        await _loop.PostAsync(begin, cancellationToken);
        await begin.Completion;

        // Cancels this target only: a newer one has another id.
        await using var registration =
            cancellationToken.Register(() => _loop.TryPost(new LoopActionWorkItem(() => CancelIfPending(session, id)))
            );

        return await completion.Task;
    }

    public void Cancel(GameSession session)
    {
        if (TakePending(session) is not { } pending)
        {
            return;
        }

        _sender.TrySend(session.SessionId, TargetCursorPacket.Cancel());
        Invoke(session, pending.Callback, TargetResult.Canceled(TargetCancelType.Canceled));
    }

    public bool TryComplete(GameSession session, int cursorId, TargetResult result)
    {
        var state = session.Get(TargetSessionKeys.State);

        if (state?.Pending is not { } pending || pending.Id != cursorId)
        {
            return false;
        }

        state.Pending = null;
        Invoke(session, pending.Callback, result);

        return true;
    }

    public void OnSessionClosed(GameSession session)
    {
        if (TakePending(session) is { } pending)
        {
            Invoke(session, pending.Callback, TargetResult.Canceled(TargetCancelType.Disconnected));
        }
    }

    private void CancelIfPending(GameSession session, int id)
    {
        if (id != 0 && session.Get(TargetSessionKeys.State)?.Pending?.Id == id)
        {
            Cancel(session);
        }
    }

    private static PendingTarget? TakePending(GameSession session)
    {
        var state = session.Get(TargetSessionKeys.State);
        var pending = state?.Pending;

        if (state is not null)
        {
            state.Pending = null;
        }

        return pending;
    }

    private static TargetState State(GameSession session)
    {
        var state = session.Get(TargetSessionKeys.State);

        if (state is null)
        {
            state = new();
            session.Set(TargetSessionKeys.State, state);
        }

        return state;
    }

    private void Invoke(GameSession session, Action<GameSession, TargetResult> callback, TargetResult result)
    {
        try
        {
            callback(session, result);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "A target callback of session {SessionId} failed", session.SessionId);
        }
    }
}
