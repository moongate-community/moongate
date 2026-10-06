using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The context menu a client shows on a mobile or an item: the server's own entries, then the ones the Lua
///     script of that NPC or item adds.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IContextMenuService
{
    /// <summary>
    ///     Sends the session the menu of <paramref name="target" /> and keeps it for the choice. False, with nothing
    ///     sent, for a target the player's character cannot see or is too far from, and for one with no entry.
    /// </summary>
    bool Request(GameSession session, Serial target);

    /// <summary>
    ///     Runs the entry the player chose from the menu last sent to it, which is forgotten whatever follows. False,
    ///     with nothing run, when the choice is not of that menu, the entry is greyed out, or the target is no longer
    ///     there to see or in the entry's range.
    /// </summary>
    bool Select(GameSession session, Serial target, int index);
}
