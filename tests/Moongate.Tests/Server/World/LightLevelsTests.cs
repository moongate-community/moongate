using Moongate.Server.Abstractions.Data.Internal;

namespace Moongate.Tests.Server.World;

public class LightLevelsTests
{
    [Fact]
    public void ForTime_Dawn_RampsFromNightToDay()
    {
        var start = LightLevels.ForTime(4, 0);
        var middle = LightLevels.ForTime(5, 0);
        var end = LightLevels.ForTime(5, 59);

        Assert.Equal(LightLevels.Night, start);
        Assert.True(middle < start, "an hour into dawn it must be lighter than at 04:00");
        Assert.True(end < middle, "and lighter still by 05:59");
    }

    [Theory, InlineData(6, 0), InlineData(12, 0), InlineData(21, 59)]

    // the first minute of full day
     // the last
    public void ForTime_Daytime_IsFullDay(int hours, int minutes)
        => Assert.Equal(LightLevels.Day, LightLevels.ForTime(hours, minutes));

    [Fact]
    public void ForTime_Dusk_RampsFromDayToNight()
    {
        var start = LightLevels.ForTime(22, 0);
        var middle = LightLevels.ForTime(23, 0);

        Assert.Equal(LightLevels.Day, start);
        Assert.True(middle > start, "an hour into dusk it must be darker than at 22:00");
    }

    [Theory, InlineData(0, 0), InlineData(3, 59)]

    // midnight
     // the last minute of full night
    public void ForTime_SmallHours_AreFullNight(int hours, int minutes)
        => Assert.Equal(LightLevels.Night, LightLevels.ForTime(hours, minutes));

    [Fact]
    public void Levels_AreTheClassicFour()
    {
        Assert.Equal(0, LightLevels.Day);
        Assert.Equal(12, LightLevels.Night);
        Assert.Equal(26, LightLevels.Dungeon);
        Assert.Equal(9, LightLevels.Jail);
    }
}
