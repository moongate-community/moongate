using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Opens gumps on players and hands their answers back, checked: an answer for a gump the player was not sent, or
///     with a button, switch or text entry the gump did not offer, or a text longer than 239 characters, is dropped.
///     Call it on the game loop.
/// </summary>
public interface IGumpService : ISessionClosedListener
{
    /// <summary>
    ///     Sends <paramref name="gump" />, compressed (0xDD) for clients from 5.0.0a and plain (0xB0) for older ones; a gump
    ///     with the same id already open on the player is closed first.
    /// </summary>
    void Open(GameSession session, GumpInstance gump);

    /// <summary>
    ///     Closes the player's gump <paramref name="id" />; its answer is not handed back.
    /// </summary>
    /// <returns>
    ///     False when no such gump is open.
    /// </returns>
    bool Close(GameSession session, string id);

    /// <summary>
    ///     Hands the player's answer to the gump it belongs to, once, if it is valid.
    /// </summary>
    void Respond(GameSession session, GumpResponsePacket packet);
}
