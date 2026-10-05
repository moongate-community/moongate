using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Sessions;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Shows players the client's hue picker (0x95) and hands the hue picked to a callback on the game loop. A player
///     has one picker at a time, and only the answer to that one is taken.
/// </summary>
public interface IHuePickerService : ISessionClosedListener
{
    /// <summary>
    ///     Shows the player the hue picker with <paramref name="graphic" /> in it; <paramref name="callback" /> runs on
    ///     the game loop with the hue picked, from 2 to 1001. It gets no hue when the picker ends without an answer:
    ///     another picker took its place, the player left, or it has no character in the world. A player that closes
    ///     the picker sends nothing, so the callback may never run. Call it on the game loop.
    /// </summary>
    void Begin(GameSession session, int graphic, Action<GameSession, int?> callback);

    /// <summary>
    ///     Ends the player's picker with <paramref name="hue" />, kept in the range a player may pick, when its id is
    ///     <paramref name="pickerId" />; false, and nothing happens, when no picker with that id is waiting. Call it on
    ///     the game loop.
    /// </summary>
    bool TryComplete(GameSession session, int pickerId, int hue);
}
