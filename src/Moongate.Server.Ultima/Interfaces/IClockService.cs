using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Types.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The time of day in the game, as ModernUO: it keeps no state, each map runs 320 game minutes after the previous one
///     and the time moves one minute later every 16 tiles east.
/// </summary>
public interface IClockService
{
    /// <summary>
    ///     Gets the time of day on <paramref name="map" /> at the column <paramref name="x" />.
    /// </summary>
    GameTime GetTime(MapType map, int x);

    /// <summary>
    ///     Gets how many game days <paramref name="map" /> has lived since the world start, at its west edge.
    /// </summary>
    long GetDay(MapType map);

    /// <summary>
    ///     Gets the phase of a moon, as ModernUO's spyglass: <paramref name="moon" /> is Felucca or Trammel, read on that
    ///     map's clock at the column <paramref name="x" />. Felucca turns every 10 game minutes, Trammel every 30.
    /// </summary>
    MoonPhaseType GetMoonPhase(MapType moon, int x);
}
