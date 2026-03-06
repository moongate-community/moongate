using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Spatial;

public class WeatherServiceTests
{
    private sealed class NullTimerService : ITimerService
    {
        public void ProcessTick() { }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
            => Guid.NewGuid().ToString("N");

        public void UnregisterAllTimers() { }

        public bool UnregisterTimer(string timerId)
            => false;

        public int UnregisterTimersByName(string name)
            => 0;

        public int UpdateTicksDelta(long timestampMilliseconds)
            => 0;
    }

    [Test]
    public void GenerateSnapshot_ShouldApplyColdAdjustment()
    {
        var service = CreateService();
        var weather = new JsonWeather
        {
            Id = 2,
            Name = "Coldland",
            MinTemp = 10,
            MaxTemp = 10,
            ColdChance = 100,
            ColdIntensity = 3
        };

        var snapshot = service.GenerateSnapshot(weather, new(10));

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.BaseTemperature, Is.EqualTo(10));
                Assert.That(snapshot.AdjustedTemperature, Is.EqualTo(7));
                Assert.That(snapshot.Condition, Is.EqualTo(JsonWeatherCondition.Clear));
                Assert.That(snapshot.Type, Is.EqualTo(WeatherType.None));
            }
        );
    }

    [Test]
    public void GenerateSnapshot_ShouldClampEffectCountTo70()
    {
        var service = CreateService();
        var weather = new JsonWeather
        {
            Id = 3,
            Name = "Stormland",
            MinTemp = 5,
            MaxTemp = 5,
            StormChance = 100,
            StormIntensity = new() { Min = 120, Max = 120 },
            StormTempDrop = 1
        };

        var snapshot = service.GenerateSnapshot(weather, new(5));

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.Condition, Is.EqualTo(JsonWeatherCondition.Storm));
                Assert.That(snapshot.Type, Is.EqualTo(WeatherType.Storm));
                Assert.That(snapshot.EffectIntensity, Is.EqualTo(120));
                Assert.That(snapshot.EffectCount, Is.EqualTo((byte)70));
            }
        );
    }

    [Test]
    public void GenerateSnapshot_ShouldResolveRainValues()
    {
        var service = CreateService();
        var weather = new JsonWeather
        {
            Id = 1,
            Name = "Rainland",
            MinTemp = 10,
            MaxTemp = 10,
            RainChance = 100,
            RainIntensity = new() { Min = 5, Max = 5 },
            RainTempDrop = 2
        };

        var snapshot = service.GenerateSnapshot(weather, new(42));

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshot.WeatherId, Is.EqualTo(1));
                Assert.That(snapshot.WeatherName, Is.EqualTo("Rainland"));
                Assert.That(snapshot.BaseTemperature, Is.EqualTo(10));
                Assert.That(snapshot.AdjustedTemperature, Is.EqualTo(10));
                Assert.That(snapshot.Condition, Is.EqualTo(JsonWeatherCondition.Rain));
                Assert.That(snapshot.EffectIntensity, Is.EqualTo(5));
                Assert.That(snapshot.TemperatureDrop, Is.EqualTo(2));
                Assert.That(snapshot.EffectiveTemperature, Is.EqualTo(8));
                Assert.That(snapshot.Type, Is.EqualTo(WeatherType.Rain));
                Assert.That(snapshot.EffectCount, Is.EqualTo((byte)5));
            }
        );
    }

    private static WeatherService CreateService(RegionDataLoaderTestSpatialWorldService? spatial = null)
        => new(
            new NullTimerService(),
            spatial ?? new RegionDataLoaderTestSpatialWorldService(),
            new BasePacketListenerTestOutgoingPacketQueue(),
            new FakeGameNetworkSessionService()
        );
}
