using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Spawns;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     UOX3's region spawns from <c>templates/spawns</c>: every region keeps up to its max NPCs alive, spawning a few at a
///     time between its min and max minutes, and tells the game masters and administrators what it spawned.
/// </summary>
public interface ISpawnRegionService : IMoongateStartupService
{
    /// <summary>
    ///     Gets the spawn regions with an area over <paramref name="x" />, <paramref name="y" /> of
    ///     <paramref name="map" />, in file order. Call it off the game loop.
    /// </summary>
    Task<IReadOnlyList<SpawnRegionStatus>> RegionsAtAsync(MapType map, int x, int y);
}
