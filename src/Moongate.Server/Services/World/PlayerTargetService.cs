using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types.World;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Holds the one target request a session can have outstanding, and turns the client's answer into
/// a decision rather than a packet.
/// </summary>
public sealed class PlayerTargetService : IPlayerTargetService
{
    /// <summary>
    /// The coordinate the client sends when the player dismissed the cursor instead of picking.
    /// </summary>
    private const int NothingPicked = 0xFFFF;

    private readonly ILogger _logger = Log.ForContext<PlayerTargetService>();
    private readonly Dictionary<long, PendingTarget> _pending = [];

    private uint _nextCursorId = 1;

    private readonly record struct PendingTarget(
        uint CursorId,
        TargetSelectionType Selection,
        Action<TargetResult> OnTarget
    );

    public bool Cancel(PlayerSession session)
    {
        if (!_pending.TryGetValue(session.SessionId, out var pending))
        {
            return false;
        }

        _pending.Remove(session.SessionId);

        session.Send(new TargetCursorPacket(pending.CursorId, pending.Selection, TargetCursorType.Cancel));
        pending.OnTarget(TargetResult.Cancelled);

        return true;
    }

    public void Forget(PlayerSession session)
        => _pending.Remove(session.SessionId);

    public TargetResultType Handle(PlayerSession session, TargetCursorResponsePacket packet)
    {
        if (!_pending.TryGetValue(session.SessionId, out var pending) || pending.CursorId != packet.CursorId)
        {
            _logger.Debug(
                "Ignoring target answer for cursor {CursorId} from session {SessionId}: nothing pending matches it",
                packet.CursorId,
                session.SessionId
            );

            return TargetResultType.Cancelled;
        }

        _pending.Remove(session.SessionId);

        var result = Interpret(pending, packet);

        pending.OnTarget(result);

        return result.Type;
    }

    public uint Request(PlayerSession session, TargetSelectionType selection, Action<TargetResult> onTarget)
    {
        // Superseding rather than accumulating: the client only has one cursor, so the previous
        // request can no longer be answered and saying otherwise would be a lie.
        Supersede(session);

        var cursorId = _nextCursorId++;

        _pending[session.SessionId] = new(cursorId, selection, onTarget);

        session.Send(new TargetCursorPacket(cursorId, selection, TargetCursorType.Neutral));

        return cursorId;
    }

    /// <summary>
    /// Nothing picked means the player dismissed the cursor. Everything else is an object when an
    /// entity was clicked, and a location otherwise — a location request never carries a serial.
    /// </summary>
    private static TargetResult Interpret(PendingTarget pending, TargetCursorResponsePacket packet)
    {
        if (packet.Location.X == NothingPicked)
        {
            return TargetResult.Cancelled;
        }

        if (packet.Clicked != Serial.Zero)
        {
            return new(TargetResultType.Object, packet.Clicked, packet.Location, packet.Graphic);
        }

        return new(TargetResultType.Location, Serial.Zero, packet.Location, packet.Graphic);
    }

    private void Supersede(PlayerSession session)
    {
        if (!_pending.Remove(session.SessionId, out var pending))
        {
            return;
        }

        pending.OnTarget(TargetResult.Cancelled);
    }
}
