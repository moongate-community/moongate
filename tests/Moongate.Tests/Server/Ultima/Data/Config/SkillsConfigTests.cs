using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class SkillsConfigTests
{
    [Fact]
    public void Defaults_AreModernUOsClassicOnes()
    {
        var config = new SkillsConfig();

        Assert.Equal((700, true, 100, 225, 10.0), (config.TotalCap, config.GainEnabled, config.StatMax, config.StatCap, config.StatGainMinutes));
        config.Validate();
    }

    [Theory]
    [InlineData(0, 100, 225, 10.0)]
    [InlineData(700, 0, 225, 10.0)]
    [InlineData(700, 100001, 225, 10.0)]
    [InlineData(700, 100, 29, 10.0)]
    [InlineData(700, 100, 100001, 10.0)]
    [InlineData(700, 100, 225, -1.0)]
    [InlineData(700, 100, 225, 1441.0)]
    public void Validate_ABadNumber_Throws(int totalCap, int statMax, int statCap, double minutes)
    {
        var config = new SkillsConfig { TotalCap = totalCap, StatMax = statMax, StatCap = statCap, StatGainMinutes = minutes };

        var error = Assert.Throws<InvalidOperationException>(config.Validate);

        Assert.Contains("ultima.skills.", error.Message);
    }

    [Fact]
    public void Validate_NoWaitBetweenGains_IsAllowed()
    {
        new SkillsConfig { StatGainMinutes = 0 }.Validate();
    }
}
