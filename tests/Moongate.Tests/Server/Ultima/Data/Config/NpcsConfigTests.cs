using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class NpcsConfigTests
{
    [Fact]
    public void ThinkIntervalMs_Default_Is500()
    {
        Assert.Equal(500, new NpcsConfig().ThinkIntervalMs);
    }

    [Theory, InlineData(50), InlineData(500), InlineData(60000)]
    public void Validate_IntervalInRange_Passes(int interval)
    {
        new NpcsConfig { ThinkIntervalMs = interval }.Validate();
    }

    [Theory, InlineData(49), InlineData(0), InlineData(-1), InlineData(60001)]
    public void Validate_IntervalOutOfRange_Throws(int interval)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new NpcsConfig { ThinkIntervalMs = interval }.Validate()
        );

        Assert.Contains("ultima.npcs.think_interval_ms", exception.Message);
    }

    [Fact]
    public void SenseRange_Default_Is8()
    {
        Assert.Equal(8, new NpcsConfig().SenseRange);
    }

    [Theory, InlineData(1), InlineData(24)]
    public void Validate_SenseRangeInRange_Passes(int range)
    {
        new NpcsConfig { SenseRange = range }.Validate();
    }

    [Theory, InlineData(0), InlineData(25)]
    public void Validate_SenseRangeOutOfRange_Throws(int range)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new NpcsConfig { SenseRange = range }.Validate());

        Assert.Contains("ultima.npcs.sense_range", exception.Message);
    }

    [Fact]
    public void UltimaConfig_NullNpcs_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new UltimaConfig { Npcs = null! }.Validate());

        Assert.Contains("ultima.npcs", exception.Message);
    }
}
