using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Session;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Validates and applies player movement: pacing, region gating, tile walkability, persistence,
/// spatial re-indexing, and broadcasting — replying to the mover and notifying nearby players.
/// <para>
/// Steps that arrive ahead of schedule are not refused outright: a small slack absorbs network
/// jitter, and past it a step waits in the session's queue until it comes due. Something has to run
/// <see cref="DrainQueue" /> on the game loop for that queue to empty.
/// </para>
/// </summary>
public interface IMovementService
{
    /// <summary>
    /// Attempts to turn or step <paramref name="session" />'s character in <paramref name="direction" />,
    /// validating <paramref name="sequence" /> against the session's rate-limit state. Always replies to
    /// the mover (ack or reject) and, on success, broadcasts to nearby players. No-ops if the session has
    /// no character attached yet.
    /// </summary>
    void TryMove(PlayerSession session, DirectionType direction, byte sequence);

    /// <summary>
    /// Runs the steps <paramref name="session" /> has waiting that have come due, oldest first,
    /// stopping at the first that has not. Called every frame; without it the tail of a queue would
    /// sit there until the player moved again, which reads as the character stopping a step short.
    /// </summary>
    void DrainQueue(PlayerSession session);

    /// <summary>
    /// Attempts to turn or step an NPC with a configured brain. Uses the authoritative movement rules without
    /// client sequence or timing state, returning <see langword="true" /> only when the move is accepted.
    /// </summary>
    bool TryMoveNpc(Serial mobileId, DirectionType direction);
}
