using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Types.World;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Raises the client's target cursor and routes the answer back.
/// <para>
/// One pending request per session, not a keyed collection. A UO client has a single target
/// cursor — it is a modal state, and asking for another replaces what is on screen — so a server
/// holding several models something that cannot exist. Asking again supersedes the previous
/// request, whose callback is invoked as <see cref="TargetResultType.Cancelled" />, which is the
/// truth: the player can no longer answer it.
/// </para>
/// </summary>
public interface IPlayerTargetService
{
    /// <summary>
    /// Takes the cursor down and reports the pending request as cancelled. Returns false when
    /// nothing was pending.
    /// </summary>
    bool Cancel(PlayerSession session);

    /// <summary>Drops a session's pending request without invoking it, on logout.</summary>
    void Forget(PlayerSession session);

    /// <summary>
    /// Handles an answer from the client, returning what it was taken to mean. An answer that does
    /// not match the session's pending request is dropped — unlike a gump's fabricated button this
    /// is ordinary traffic, since a superseded cursor can still be clicked.
    /// </summary>
    TargetResultType Handle(PlayerSession session, TargetCursorResponsePacket packet);

    /// <summary>
    /// Raises a cursor for this session and returns the id correlating its answer. Any request
    /// already pending is cancelled first.
    /// </summary>
    uint Request(PlayerSession session, TargetSelectionType selection, Action<TargetResult> onTarget);
}
