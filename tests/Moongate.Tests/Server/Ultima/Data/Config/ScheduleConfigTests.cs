using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class ScheduleConfigTests
{
    [Fact]
    public void Default_IsTheSystemZone()
    {
        var config = new ScheduleConfig();

        Assert.Equal("", config.TimeZone);
        Assert.Same(TimeZoneInfo.Local, config.Resolve());
        config.Validate();
    }

    [Fact]
    public void AKnownZone_IsResolved()
    {
        var config = new ScheduleConfig { TimeZone = "UTC" };

        Assert.Equal(TimeSpan.Zero, config.Resolve().BaseUtcOffset);
        config.Validate();
    }

    [Fact]
    public void AnUnknownZone_StopsTheValidation_NamingTheSetting()
    {
        var config = new ScheduleConfig { TimeZone = "Mars/Olympus" };

        var message = Assert.Throws<InvalidOperationException>(config.Validate).Message;

        Assert.Contains("ultima.schedule.time_zone", message);
        Assert.Contains("Mars/Olympus", message);
    }

    [Fact]
    public void UltimaConfig_HasTheScheduleSection_AndValidatesIt()
    {
        var config = new UltimaConfig();

        Assert.Equal("", config.Schedule.TimeZone);

        config.Schedule.TimeZone = "Mars/Olympus";

        Assert.Contains("ultima.schedule.time_zone", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
