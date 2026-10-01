using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The season each player's client shows (0xBC): its region's, else its map's. A map's season is the one set at
///     runtime, else its <c>season</c> in <c>maps.toml</c>, rotating with the game days when
///     <c>ultima.world.season_rotation</c> is on. A new season is followed by the light and the weather, which the
///     packet resets in the client.
/// </summary>
public interface ISeasonService : IMoongateStartupService, IRegionChangeListener
{
    /// <summary>
    ///     Gets the season of where the player stands: its region's, else its map's.
    /// </summary>
    SeasonType SeasonOf(MobileEntity player);

    /// <summary>
    ///     Gets the season of the map: the one set at runtime, else its rotated <c>maps.toml</c> season.
    /// </summary>
    SeasonType SeasonOf(MapType map);

    /// <summary>
    ///     Gets the season for a character entering the world and records it as sent, so it is sent again only when it
    ///     changes.
    /// </summary>
    SeasonType SeasonOnLogin(MobileEntity character);

    /// <summary>
    ///     Sets, or with null clears, the map's season until the restart, and sends it to the map's players. Call it on
    ///     the game loop.
    /// </summary>
    void SetOverride(MapType map, SeasonType? season);
}
