using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Session;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Validates and applies player movement: rate limiting, region gating, tile walkability, persistence,
/// spatial re-indexing, and broadcasting — replying to the mover and notifying nearby players.
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
}
