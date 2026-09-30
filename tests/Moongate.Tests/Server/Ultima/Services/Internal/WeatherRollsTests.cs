using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Weather;
using Moongate.Tests.TestSupport.Random;

namespace Moongate.Tests.Server.Ultima.Services.Internal;

public sealed class WeatherRollsTests
{
    private static readonly WeatherContent Profile = new()
    {
        Name = "temperate", RainChance = 20, SnowChance = 10, StormChance = 5, SnowThreshold = 5, MinTemperature = 10,
        MaxTemperature = 20, ColdChance = 10, ColdTemperature = -10, HeatChance = 10, HeatTemperature = 30,
        RainTemperatureDrop = 5, StormTemperatureDrop = 10
    };

    [Fact]
    public void DayTemperature_ANormalDay_IsBetweenMinAndMax()
    {
        // heat roll 50 (miss), cold roll 50 (miss), temperature 15.
        Assert.Equal(15, WeatherRolls.DayTemperature(Profile, new ScriptedRandom(50, 50, 15)));
    }

    [Fact]
    public void DayTemperature_AHotDay_IsBetweenMaxAndTheHeatTemperature()
    {
        Assert.Equal(27, WeatherRolls.DayTemperature(Profile, new ScriptedRandom(1, 27)));
    }

    [Fact]
    public void DayTemperature_AColdDay_IsBetweenTheColdTemperatureAndMin()
    {
        Assert.Equal(-3, WeatherRolls.DayTemperature(Profile, new ScriptedRandom(50, 1, -3)));
    }

    [Fact]
    public void Hour_AStormHitsFirst_AndLowersTheTemperatureByItsDrop()
    {
        // storm 1 (hit), density 40.
        Assert.Equal(new WeatherState(WeatherKindType.Storm, 40, 5), WeatherRolls.Hour(Profile, 15, new ScriptedRandom(1, 40)));
    }

    [Fact]
    public void Hour_SnowNeedsATemperatureBelowTheThreshold_ElseItRains()
    {
        // storm miss, snow hit, density 30.
        Assert.Equal(new WeatherState(WeatherKindType.Snow, 30, 2), WeatherRolls.Hour(Profile, 2, new ScriptedRandom(99, 1, 30)));

        // storm miss, snow hit but too warm, rain hit, density 30.
        Assert.Equal(new WeatherState(WeatherKindType.Rain, 30, 10), WeatherRolls.Hour(Profile, 15, new ScriptedRandom(99, 1, 1, 30)));
    }

    [Fact]
    public void Hour_NothingHits_IsDry()
    {
        Assert.Equal(new WeatherState(WeatherKindType.None, 0, 15), WeatherRolls.Hour(Profile, 15, new ScriptedRandom(99)));
    }

    [Fact]
    public void Hour_TheTemperatureIsASignedByte()
    {
        Assert.Equal(-128, WeatherRolls.Hour(Profile, -125, new ScriptedRandom(1, 40)).Temperature);
    }
}
