using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Shows map items to players and applies the changes players make to their course.
/// </summary>
public interface IMapDisplayService
{
    /// <summary>
    ///     Shows the map item to the player of the session: its area, its course and whether the player may change it.
    ///     False, with nothing sent, for an item with no area or a map of another facet than Felucca and Trammel to a
    ///     client older than 7.0.13.
    /// </summary>
    bool Display(GameSession session, ItemEntity map);

    /// <summary>
    ///     Applies a change the player made to the course of a map item: ignored unless the map is in the player's
    ///     backpack or within 2 tiles, movable and not protected, and, but for the toggle, while it may be changed.
    /// </summary>
    void Handle(GameSession session, MapCommandRequestPacket packet);
}
