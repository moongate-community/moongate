using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class JailConfigTests
{
    [Fact]
    public void Defaults_Are500GoldAnd30Days()
    {
        var config = new JailConfig();

        Assert.Equal((500, 30), (config.FineGold, config.MaxDays));
        config.Validate();
    }

    [Theory,
     InlineData(-1, 30, "ultima.jail.fine_gold"),
     InlineData(1_000_000_001, 30, "ultima.jail.fine_gold"),
     InlineData(500, 0, "ultima.jail.max_days"),
     InlineData(500, 3651, "ultima.jail.max_days")]
    public void Validate_AValueOutOfRange_NamesTheSetting(int fine, int days, string setting)
    {
        var config = new JailConfig { FineGold = fine, MaxDays = days };

        Assert.Contains(setting, Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(0, 1), InlineData(1_000_000_000, 3650)]
    public void Validate_TheLimits_AreAccepted(int fine, int days)
    {
        new JailConfig { FineGold = fine, MaxDays = days }.Validate();
    }

    [Fact]
    public void UltimaConfig_HasTheJailSection_AndValidatesIt()
    {
        var config = new UltimaConfig();

        Assert.Equal(500, config.Jail.FineGold);

        config.Jail.MaxDays = 0;

        Assert.Contains("ultima.jail.max_days", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
