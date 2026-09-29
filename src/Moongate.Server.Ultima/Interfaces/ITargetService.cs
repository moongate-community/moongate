using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Asks players to pick something with the target cursor (0x6C) and hands the result to a callback on the game
///     loop, or to an awaiting command. A player has one target at a time.
/// </summary>
public interface ITargetService : ISessionClosedListener
{
    /// <summary>
    ///     Shows the player the target cursor; <paramref name="callback" /> runs on the game loop with the result. A
    ///     target the player already had ends as overridden; without a character in the world the callback gets a
    ///     cancel at once. Call it on the game loop.
    /// </summary>
    void Begin(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        Action<GameSession, TargetResult> callback
    );

    /// <summary>
    ///     Begins a target on the game loop and completes with its result; cancelling
    ///     <paramref name="cancellationToken" /> cancels this target only. Call it off the game loop.
    /// </summary>
    Task<TargetResult> RequestAsync(
        GameSession session,
        TargetCursorType cursor,
        TargetFlagsType flags,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Takes the cursor away from the player and ends the target as cancelled. Call it on the game loop.
    /// </summary>
    void Cancel(GameSession session);

    /// <summary>
    ///     Ends the player's target with <paramref name="result" /> when its id is <paramref name="cursorId" />; false,
    ///     and nothing happens, when no target with that id is waiting. Call it on the game loop.
    /// </summary>
    bool TryComplete(GameSession session, int cursorId, TargetResult result);
}
