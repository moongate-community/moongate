using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Regions;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.World;

public class LightServiceTests
{
    [Fact]
    public void LevelFor_InATown_FollowsTheClock()
    {
        var regions = new RegionService();
        regions.Register(Region("TownRegion", 100, 100));

        // The epoch is midnight, which the curve puts in full night.
        var service = new LightService(regions, new FixedTimeProvider(GameClock.Epoch));

        Assert.Equal(LightLevels.Night, service.LevelFor(0, new(0, 0, 0)));
    }

    [Fact]
    public void LevelFor_InsideADungeon_IsAlwaysDark()
    {
        var regions = new RegionService();
        regions.Register(Region("DungeonRegion", 100, 100));

        var service = new LightService(regions, new FixedTimeProvider(UoNoon));

        Assert.Equal(LightLevels.Dungeon, service.LevelFor(0, new(100, 100, 0)));
    }

    [Fact]
    public void LevelFor_InsideAJail_IsTheJailLevel()
    {
        var regions = new RegionService();
        regions.Register(Region("JailRegion", 100, 100));

        var service = new LightService(regions, new FixedTimeProvider(UoNoon));

        Assert.Equal(LightLevels.Jail, service.LevelFor(0, new(100, 100, 0)));
    }

    [Fact]
    public void LevelFor_OutsideAnyRegion_FollowsTheClock()
    {
        var service = new LightService(new RegionService(), new FixedTimeProvider(GameClock.Epoch));

        Assert.Equal(LightLevels.Night, service.LevelFor(0, new(0, 0, 0)));
    }

    [Fact]
    public void Override_WhenSet_WinsEverywhere()
    {
        var regions = new RegionService();
        regions.Register(Region("DungeonRegion", 100, 100));

        var service = new LightService(regions, new FixedTimeProvider(UoNoon)) { Override = 3 };

        Assert.Equal(3, service.LevelFor(0, new(100, 100, 0)));
    }

    /// <summary>UO noon: 12 UO hours is 720 UO minutes, and each is 5 real seconds.</summary>
    private static DateTimeOffset UoNoon => GameClock.Epoch.AddHours(1);

    private static RegionDefinition Region(string type, int x, int y)
        => new()
        {
            Type = type,
            Name = type,
            Map = MapType.Felucca,
            Area = [new() { X1 = x - 5, Y1 = y - 5, X2 = x + 5, Y2 = y + 5 }]
        };
}
