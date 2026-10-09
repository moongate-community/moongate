using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Opens a container on the client of a player: its gump, its items, and the tooltip revision of each item.
/// </summary>
public interface IContainerViewService
{
    /// <summary>
    ///     Shows <paramref name="container" /> and the items directly inside it to <paramref name="session" />, as a double
    ///     click on a container the player carries does. It checks nothing: whoever calls it has decided the player may see.
    /// </summary>
    void Show(GameSession session, ItemEntity container);
}
