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
    ///     Gets whether <paramref name="item" /> can be used from afar by <paramref name="user" />, as the Telekinesis
    ///     spell does: it has an <c>on_use</c> in its script, or is a container the user carries or that lies on the
    ///     ground.
    /// </summary>
    bool CanUseFromAfar(MobileEntity user, ItemEntity item);

    /// <summary>
    ///     Does what a double click of the session's character on the item <paramref name="target" /> does, with no
    ///     check of how far it is: the item's <c>on_use</c>, or the contents of a container. False when nothing was done.
    /// </summary>
    bool UseFromAfar(GameSession session, Serial target);

    /// <summary>
    ///     Gets whether the mobile has a paperdoll to open: only human bodies do.
    /// </summary>
    bool HasPaperdoll(MobileEntity mobile);
}
