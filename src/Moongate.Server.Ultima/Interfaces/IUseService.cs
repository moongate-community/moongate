using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Uses a mobile or an item for a player as its double click does: the paperdoll of a mobile, the script or the
///     contents of an item.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IUseService
{
    /// <summary>
    ///     Does what a double click of the session's character on <paramref name="target" /> does, with every check
    ///     of it.
    /// </summary>
    void Use(GameSession session, Serial target);

    /// <summary>
    ///     Gets whether the mobile has a paperdoll to open: only human bodies do.
    /// </summary>
    bool HasPaperdoll(MobileEntity mobile);
}
