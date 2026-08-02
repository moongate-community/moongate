using Moongate.Server.Abstractions.Data.Internal;

namespace Moongate.Tests.Server.World;

public class GameClockTests
{
    [Fact]
    public void LocalTime_AtTheEpoch_IsMidnight()
    {
        var (hours, minutes) = GameClock.LocalTime(GameClock.Epoch, 0, 0);

        Assert.Equal(0, hours);
        Assert.Equal(0, minutes);
    }

    [Fact]
    public void LocalTime_DiffersBetweenFacets()
    {
        var first = GameClock.LocalTime(GameClock.Epoch, 0, 0);
        var second = GameClock.LocalTime(GameClock.Epoch, 1, 0);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void LocalTime_FiveRealSeconds_IsOneUoMinute()
    {
        var (hours, minutes) = GameClock.LocalTime(GameClock.Epoch.AddSeconds(5), 0, 0);

        Assert.Equal(0, hours);
        Assert.Equal(1, minutes);
    }

    [Fact]
    public void LocalTime_SixteenTilesEast_IsOneUoMinuteLater()
    {
        var west = GameClock.LocalTime(GameClock.Epoch, 0, 0);
        var east = GameClock.LocalTime(GameClock.Epoch, 0, 16);

        Assert.Equal(west.Minutes + 1, east.Minutes);
    }

    [Fact]
    public void LocalTime_TwoRealHours_IsAFullUoDay()
    {
        // 5s per UO minute means a UO day takes 120 real minutes, so we are back at midnight.
        var (hours, minutes) = GameClock.LocalTime(GameClock.Epoch.AddHours(2), 0, 0);

        Assert.Equal(0, hours);
        Assert.Equal(0, minutes);
    }

    [Fact]
    public void LocalTime_WrapsPastTwentyFour()
    {
        // 25 UO hours after the epoch is 01:00, not 25:00.
        var (hours, _) = GameClock.LocalTime(GameClock.Epoch.AddMinutes(125), 0, 0);

        Assert.Equal(1, hours);
    }
}
