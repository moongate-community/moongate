using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json.Regions;

namespace Moongate.Tests.Server.Services.Spatial;

public class LightServiceTests
{
    [Test]
    public void ComputeGlobalLightLevel_ShouldReturnNightLevelBetweenMidnightAnd4()
    {
        var service = CreateService();
        var level = service.ComputeGlobalLightLevel(new DateTime(2026, 3, 6, 2, 30, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(12));
    }

    [Test]
    public void ComputeGlobalLightLevel_ShouldInterpolateAtDawnBetween4And6()
    {
        var service = CreateService();
        var level = service.ComputeGlobalLightLevel(new DateTime(2026, 3, 6, 5, 0, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(6));
    }

    [Test]
    public void ComputeGlobalLightLevel_ShouldReturnDayLevelBetween6And22()
    {
        var service = CreateService();
        var level = service.ComputeGlobalLightLevel(new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(0));
    }

    [Test]
    public void ComputeGlobalLightLevel_ShouldInterpolateAtDuskBetween22And24()
    {
        var service = CreateService();
        var level = service.ComputeGlobalLightLevel(new DateTime(2026, 3, 6, 23, 0, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(6));
    }

    [Test]
    public void ComputeGlobalLightLevel_WithMapOffset_ShouldMatchModernUoClockStyle()
    {
        var service = CreateService();
        var levelMap0 = service.ComputeGlobalLightLevel(0, new(0, 0, 0), new DateTime(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var levelMap1 = service.ComputeGlobalLightLevel(1, new(0, 0, 0), new DateTime(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Multiple(
            () =>
            {
                Assert.That(levelMap0, Is.EqualTo(12));
                Assert.That(levelMap1, Is.EqualTo(4));
            }
        );
    }

    [Test]
    public void ComputeGlobalLightLevel_WithCustomClockConfig_ShouldUseConfiguredValues()
    {
        var config = new MoongateSpatialConfig
        {
            LightWorldStartUtc = "2000-01-01T00:00:00Z",
            LightSecondsPerUoMinute = 10
        };
        var service = CreateService(config: config);

        var level = service.ComputeGlobalLightLevel(
            0,
            new Point3D(0, 0, 0),
            new DateTime(2000, 1, 1, 0, 50, 0, DateTimeKind.Utc)
        );

        Assert.That(level, Is.EqualTo(6));
    }

    [Test]
    public void ComputeGlobalLightLevel_WithXOffset_ShouldMatchModernUoClockStyle()
    {
        var service = CreateService();
        var baseLevel = service.ComputeGlobalLightLevel(0, new(0, 0, 0), new DateTime(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var shiftedLevel = service.ComputeGlobalLightLevel(0, new(4000, 0, 0), new DateTime(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Multiple(
            () =>
            {
                Assert.That(baseLevel, Is.EqualTo(12));
                Assert.That(shiftedLevel, Is.EqualTo(11));
            }
        );
    }

    [Test]
    public void ComputeGlobalLightLevel_InDungeonRegion_ShouldReturnDungeonLevel()
    {
        var spatial = new RegionDataLoaderTestSpatialWorldService
        {
            ResolvedRegion = new JsonDungeonRegion()
        };
        var service = CreateService(spatial);

        var level = service.ComputeGlobalLightLevel(0, new(100, 100, 0), new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(26));
    }

    [Test]
    public void ComputeGlobalLightLevel_InJailRegion_ShouldReturnJailLevel()
    {
        var spatial = new RegionDataLoaderTestSpatialWorldService
        {
            ResolvedRegion = new JsonJailRegion()
        };
        var service = CreateService(spatial);

        var level = service.ComputeGlobalLightLevel(0, new(100, 100, 0), new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(9));
    }

    [Test]
    public void ComputeGlobalLightLevel_WithForcedOverride_ShouldReturnForcedLevel()
    {
        var service = CreateService();
        service.SetGlobalLightOverride(26, applyImmediately: false);

        var level = service.ComputeGlobalLightLevel(0, new(100, 100, 0), new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(26));
    }

    [Test]
    public void ComputeGlobalLightLevel_AfterClearingForcedOverride_ShouldReturnDynamicValue()
    {
        var service = CreateService();
        service.SetGlobalLightOverride(26, applyImmediately: false);
        service.SetGlobalLightOverride(null, applyImmediately: false);

        var level = service.ComputeGlobalLightLevel(0, new(0, 0, 0), new DateTime(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(level, Is.EqualTo(12));
    }

    private sealed class NullTimerService : ITimerService
    {
        public void ProcessTick() { }

        public string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false)
            => Guid.NewGuid().ToString("N");

        public void UnregisterAllTimers() { }

        public bool UnregisterTimer(string timerId)
            => false;

        public int UnregisterTimersByName(string name)
            => 0;

        public int UpdateTicksDelta(long timestampMilliseconds)
            => 0;
    }

    private static LightService CreateService(
        RegionDataLoaderTestSpatialWorldService? spatial = null,
        MoongateSpatialConfig? config = null
    )
        => new(
            new NullTimerService(),
            spatial ?? new RegionDataLoaderTestSpatialWorldService(),
            new BasePacketListenerTestOutgoingPacketQueue(),
            new FakeGameNetworkSessionService(),
            config
        );
}
