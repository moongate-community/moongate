using Moongate.Server.Ultima.Data.Spawns;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Spawns;

/// <summary>
///     Gives <see cref="Here" /> for any spot and records the spots asked about.
/// </summary>
public sealed class StubSpawnRegionService : ISpawnRegionService
{
    public List<SpawnRegionStatus> Here { get; } = [];

    public List<(MapType Map, int X, int Y)> Asked { get; } = [];

    public (int Regions, int Missing) Fill { get; set; }

    public int FillAllCalls { get; private set; }

    public Task<(int Regions, int Missing)> FillAllAsync()
    {
        FillAllCalls++;

        return Task.FromResult(Fill);
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SpawnRegionStatus>> RegionsAtAsync(MapType map, int x, int y)
    {
        Asked.Add((map, x, y));

        return Task.FromResult<IReadOnlyList<SpawnRegionStatus>>(Here);
    }
}
