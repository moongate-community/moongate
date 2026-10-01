using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Answers <see cref="Here" /> for every player and <see cref="MapSeason" /> for every map, and records the overrides.
/// </summary>
public sealed class StubSeasonService : ISeasonService
{
    public SeasonType Here { get; set; } = SeasonType.Fall;

    public SeasonType MapSeason { get; set; } = SeasonType.Summer;

    public List<(MapType Map, SeasonType? Season)> Overrides { get; } = [];

    public int SetOnThread { get; private set; }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
    }

    public void Left(Serial player)
    {
    }

    public SeasonType SeasonOf(MobileEntity player)
    {
        return Here;
    }

    public SeasonType SeasonOf(MapType map)
    {
        return MapSeason;
    }

    public SeasonType SeasonOnLogin(MobileEntity character)
    {
        return Here;
    }

    public void SetOverride(MapType map, SeasonType? season)
    {
        Overrides.Add((map, season));
        SetOnThread = System.Environment.CurrentManagedThreadId;
    }
}
