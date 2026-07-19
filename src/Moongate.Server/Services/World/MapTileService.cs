using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default <see cref="IMapTileService" />: a single-tile port of the classic "CanFit" walkability
/// check. Constants (person height, step height) are taken from ModernUO's
/// <c>Engines/Pathing/Movement.cs</c>. Deliberately does not port ModernUO's diagonal-neighbor
/// corner-cutting check, other-mobile obstacles, or swim/fly/door special-casing — single tile only.
/// </summary>
public sealed class MapTileService : IMapTileService
{
    // A mobile's standing headroom requirement (ModernUO: MovementImpl.PersonHeight).
    private const int PersonHeight = 16;

    // Max Z difference between where a mobile stands and a static/item's base for it to be
    // directly climbable in one step (ModernUO: MovementImpl.StepHeight).
    private const int StepHeight = 2;

    private readonly IUltimaMapProvider _mapProvider;

    public MapTileService(IUltimaMapProvider mapProvider)
    {
        _mapProvider = mapProvider;
    }

    public bool TryGetWalkableZ(int mapId, int x, int y, int currentZ, IReadOnlyList<ItemEntity> groundItems, out int newZ)
    {
        newZ = 0;
        var map = _mapProvider.Get((MapType)mapId);

        if (map is null)
        {
            return false;
        }

        var candidates = new List<int>();
        var obstacles = new List<(int Bottom, int Top)>();

        var land = map.Tiles.GetLandTile(x, y);
        var landFlags = TileData.LandTable[land.Id].Flags;

        if ((landFlags & TileFlagType.Impassable) == 0)
        {
            candidates.Add(land.Z);
        }

        foreach (var tile in map.Tiles.GetStaticTiles(x, y))
        {
            CollectTile(tile.Id, tile.Z, currentZ, candidates, obstacles);
        }

        foreach (var item in groundItems)
        {
            // groundItems may come from a wider-than-one-tile sweep (the caller doesn't know the
            // target tile until it has already fetched candidates for every direction) — filter to
            // exactly this tile rather than assuming the caller pre-filtered.
            if (item.Position.X != x || item.Position.Y != y)
            {
                continue;
            }

            CollectTile(item.ItemId, item.Position.Z, currentZ, candidates, obstacles);
        }

        candidates.Sort((a, b) => Math.Abs(a - currentZ).CompareTo(Math.Abs(b - currentZ)));

        foreach (var candidate in candidates)
        {
            if (!HasHeadroom(candidate, obstacles))
            {
                continue;
            }

            if (PathBlocked(currentZ, candidate, obstacles))
            {
                continue;
            }

            newZ = candidate;

            return true;
        }

        return false;
    }

    private static void CollectTile(int itemId, int z, int currentZ, List<int> candidates, List<(int Bottom, int Top)> obstacles)
    {
        if (itemId < 0 || itemId >= TileData.ItemTable.Length)
        {
            return;
        }

        var data = TileData.ItemTable[itemId];

        if ((data.Surface || data.Bridge) && z <= currentZ + StepHeight)
        {
            candidates.Add(z + data.CalcHeight);
        }

        if (data.Impassable)
        {
            obstacles.Add((z, z + data.Height));
        }
    }

    // Only correct when paired with PathBlocked below: an obstacle whose Bottom sits below the
    // candidate surface is caught there, not here. Called in isolation, this alone cannot detect
    // that case, so do not separate the two calls in TryGetWalkableZ without re-checking this.
    private static bool HasHeadroom(int surfaceZ, List<(int Bottom, int Top)> obstacles)
    {
        var ceiling = int.MaxValue;

        foreach (var obstacle in obstacles)
        {
            if (obstacle.Bottom >= surfaceZ && obstacle.Bottom < ceiling)
            {
                ceiling = obstacle.Bottom;
            }
        }

        return ceiling - surfaceZ >= PersonHeight;
    }

    private static bool PathBlocked(int fromZ, int toZ, List<(int Bottom, int Top)> obstacles)
    {
        var low = Math.Min(fromZ, toZ);
        var high = Math.Max(fromZ, toZ);

        foreach (var obstacle in obstacles)
        {
            if (obstacle.Bottom < high && obstacle.Top > low)
            {
                return true;
            }
        }

        return false;
    }
}
