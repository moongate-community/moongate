using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Internal.Prompts;
using Moongate.Server.Ultima.Data.Prompts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the one text prompt a player may have in its session, as ModernUO keeps one per mobile: a new one ends
///     the old with no text, and so do a cancel and the session closing. Ids count up per session.
/// </summary>
public sealed class PromptService : IPromptService
{
    private readonly ILogger _logger = Log.ForContext<PromptService>();
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;

    public PromptService(IMobileService mobiles, IPacketSendService sender)
    {
        _mobiles = mobiles;
        _sender = sender;
    }

    public void Begin(GameSession session, Action<GameSession, string?> callback)
    {
        if (!session.CharacterId.IsValid || !_mobiles.IsInWorld(session.CharacterId))
        {
            Invoke(session, callback, null);

            return;
        }

        var state = session.Get(PromptSessionKeys.State);

        if (state is null)
        {
            state = new();
            session.Set(PromptSessionKeys.State, state);
        }

        var previous = state.Pending;
        state.LastId = state.LastId == int.MaxValue ? 1 : state.LastId + 1;
        state.Pending = new(state.LastId, callback);

        if (previous is not null)
        {
            Invoke(session, previous.Callback, null);
        }

        _sender.TrySend(session.SessionId, new TextPromptPacket(session.CharacterId, state.LastId));
    }

    public void Cancel(GameSession session)
    {
        if (TakePending(session) is { } pending)
        {
            Invoke(session, pending.Callback, null);
        }
    }

    public bool TryComplete(GameSession session, int promptId, string? text)
    {
        var state = session.Get(PromptSessionKeys.State);

        if (state?.Pending is not { } pending || pending.Id != promptId)
        {
            return false;
        }

        state.Pending = null;
        Invoke(session, pending.Callback, text);

        return true;
    }

    public void OnSessionClosed(GameSession session)
    {
        Cancel(session);
    }

    private static PendingPrompt? TakePending(GameSession session)
    {
        var state = session.Get(PromptSessionKeys.State);
        var pending = state?.Pending;

        if (state is not null)
        {
            state.Pending = null;
        }

        return pending;
    }

    private void Invoke(GameSession session, Action<GameSession, string?> callback, string? text)
    {
        try
        {
            callback(session, text);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "A prompt callback of session {SessionId} failed", session.SessionId);
        }
    }
}
