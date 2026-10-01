using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Weather;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The weather of every profile of <c>weather.toml</c>, rolled each game hour (and its temperature each game day), and
///     sent (0x65) to each player from its region's profile, or its map's outside every region; dry inside a building.
///     It follows the players through the region changes.
/// </summary>
public interface IWeatherService : IMoongateStartupService, IRegionChangeListener
{
    /// <summary>
    ///     Gets the profile that applies to <paramref name="player" />: its region's, else its map's.
    /// </summary>
    string ProfileOf(MobileEntity player);

    /// <summary>
    ///     Gets the weather of the profile this hour; dry for an unknown profile.
    /// </summary>
    WeatherState StateOf(string profile);

    /// <summary>
    ///     Makes it <paramref name="kind" /> in <paramref name="profile" /> until the next game hour.
    /// </summary>
    void Force(string profile, WeatherKindType kind);

    /// <summary>
    ///     Sends the player its weather again, even unchanged: a season packet (0xBC) stops the client's weather.
    /// </summary>
    void Resend(MobileEntity player);
}
