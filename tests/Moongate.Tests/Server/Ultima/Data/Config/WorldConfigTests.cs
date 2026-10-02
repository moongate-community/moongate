using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class WorldConfigTests
{
    [Fact]
    public void ViewRange_Default_Is18()
    {
        Assert.Equal(18, new WorldConfig().ViewRange);
    }

    [Theory, InlineData(5), InlineData(18), InlineData(24)]
    public void Validate_RangeTheClientSupports_Passes(int range)
    {
        new WorldConfig { ViewRange = range }.Validate();
    }

    [Theory, InlineData(4), InlineData(25), InlineData(0)]
    public void Validate_RangeTheClientDoesNotSupport_Throws(int range)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { ViewRange = range }.Validate());

        Assert.Contains("world.view_range", exception.Message);
    }

    [Fact]
    public void LightCycle_Defaults_AreModernUOs()
    {
        var world = new WorldConfig();

        Assert.Equal((5, 0, 12), (world.SecondsPerUoMinute, world.DayLight, world.NightLight));
    }

    [Theory, InlineData(0), InlineData(3601)]
    public void Validate_SecondsPerUoMinuteOutOf1To3600_Throws(int seconds)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { SecondsPerUoMinute = seconds }.Validate());

        Assert.Contains("world.seconds_per_uo_minute", exception.Message);
    }

    [Theory, InlineData(-1, 12, "day_light"), InlineData(32, 12, "day_light"), InlineData(0, -1, "night_light"), InlineData(0, 32, "night_light")]
    public void Validate_ALightOutOf0To31_Throws(int day, int night, string key)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { DayLight = day, NightLight = night }.Validate());

        Assert.Contains("world." + key, exception.Message);
    }

    [Fact]
    public void Seasons_Default_DoNotRotate_And12GameDaysEach()
    {
        var world = new WorldConfig();

        Assert.Equal((false, 12), (world.SeasonRotation, world.DaysPerSeason));
    }

    [Theory, InlineData(0), InlineData(366)]
    public void Validate_DaysPerSeasonOutOf1To365_Throws(int days)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { DaysPerSeason = days }.Validate());

        Assert.Contains("world.days_per_season", exception.Message);
    }

    [Fact]
    public void RegionLight_Defaults_AreModernUOs()
    {
        var world = new WorldConfig();

        Assert.Equal((26, 9), (world.DungeonLight, world.JailLight));
    }

    [Theory, InlineData(-1, 9, "dungeon_light"), InlineData(32, 9, "dungeon_light"), InlineData(26, -1, "jail_light"), InlineData(26, 32, "jail_light")]
    public void Validate_ARegionLightOutOf0To31_Throws(int dungeon, int jail, string key)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { DungeonLight = dungeon, JailLight = jail }.Validate());

        Assert.Contains("world." + key, exception.Message);
    }

    [Fact]
    public void LampPostLight_Default_Is6()
    {
        Assert.Equal(6, new WorldConfig().LampPostLight);
    }

    [Theory, InlineData(-1), InlineData(32)]
    public void Validate_ALampPostLightOutOf0To31_Throws(int level)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { LampPostLight = level }.Validate());

        Assert.Contains("world.lamp_post_light", exception.Message);
    }

    [Fact]
    public void PathfindingLimits_DefaultToModernUos()
    {
        var world = new WorldConfig();

        Assert.Equal((38, 1000), (world.PathfindingRange, world.PathfindingMaxNodes));
        world.Validate();
    }

    [Theory, InlineData(7), InlineData(65)]
    public void Validate_PathfindingRangeOutOf8To64_Throws(int range)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { PathfindingRange = range }.Validate());

        Assert.Contains("ultima.world.pathfinding_range", exception.Message);
    }

    [Theory, InlineData(49), InlineData(20001)]
    public void Validate_PathfindingMaxNodesOutOf50To20000_Throws(int nodes)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { PathfindingMaxNodes = nodes }.Validate());

        Assert.Contains("ultima.world.pathfinding_max_nodes", exception.Message);
    }
}
