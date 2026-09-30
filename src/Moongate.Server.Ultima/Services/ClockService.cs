using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Counts game minutes from ModernUO's world start, one every <see cref="WorldConfig.SecondsPerUoMinute" /> real
///     seconds; MapType values are ModernUO's map indexes.
/// </summary>
public sealed class ClockService : IClockService
{
    public const int MinutesPerMap = 320;
    public const int TilesPerMinute = 16;

    private static readonly DateTimeOffset WorldStart = new(1997, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _time;
    private readonly WorldConfig _world;

    public ClockService(TimeProvider time, WorldConfig world)
    {
        _time = time;
        _world = world;
    }

    public GameTime GetTime(MapType map, int x)
    {
        var elapsed = _time.GetUtcNow() - WorldStart;
        var total = (long)(elapsed.TotalSeconds / _world.SecondsPerUoMinute) +
                    (int)map * MinutesPerMap +
                    Math.Max(0, x) / TilesPerMinute;

        return new((int)(total / 60 % 24), (int)(total % 60));
    }
}
