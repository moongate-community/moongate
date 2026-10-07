using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Gives the same time of day and game day everywhere, which a test sets.
/// </summary>
public sealed class StubClockService : IClockService
{
    public GameTime Time { get; set; } = new(12, 0);

    public long Day { get; set; }

    public MoonPhaseType Moon { get; set; } = MoonPhaseType.FullMoon;

    public GameTime GetTime(MapType map, int x)
    {
        return Time;
    }

    public long GetDay(MapType map)
    {
        return Day;
    }

    public MoonPhaseType GetMoonPhase(MapType moon, int x)
    {
        return Moon;
    }
}
