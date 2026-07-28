using Moongate.Core.Geometry;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default <see cref="ILightService" />. The region wins over the hour — a dungeon is dark at noon —
/// and a GM override wins over both.
/// </summary>
public sealed class LightService : ILightService
{
    private const string DungeonRegionType = "DungeonRegion";
    private const string JailRegionType = "JailRegion";

    private readonly IRegionService _regions;
    private readonly TimeProvider _timeProvider;

    public int? Override { get; set; }

    public LightService(IRegionService regions, TimeProvider timeProvider)
    {
        _regions = regions;
        _timeProvider = timeProvider;
    }

    public int LevelFor(int mapId, Point3D position)
    {
        if (Override is { } forced)
        {
            return forced;
        }

        // MapType's own summary says its value matches the client's map index, so the cast is exact.
        var region = _regions.At((MapType)mapId, position);

        if (region is not null)
        {
            if (string.Equals(region.Type, DungeonRegionType, StringComparison.Ordinal))
            {
                return LightLevels.Dungeon;
            }

            if (string.Equals(region.Type, JailRegionType, StringComparison.Ordinal))
            {
                return LightLevels.Jail;
            }
        }

        var (hours, minutes) = GameClock.LocalTime(_timeProvider.GetUtcNow(), mapId, position.X);

        return LightLevels.ForTime(hours, minutes);
    }
}
