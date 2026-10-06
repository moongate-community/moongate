using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class RegenerationConfigTests
{
    [Fact]
    public void Defaults_AreModernUosClassicRates_AndHungerEveryFiveMinutes()
    {
        var config = new RegenerationConfig();

        Assert.Equal((11.0, 7.0, 7.0), (config.HitsSeconds, config.StaminaSeconds, config.ManaSeconds));
        Assert.Equal((true, true, 5), (config.HungerEnabled, config.ThirstEnabled, config.HungerMinutes));
        config.Validate();
    }

    [Theory,
     InlineData(0.05, 7, 7, 5, "ultima.regeneration.hits_seconds"),
     InlineData(11, 3601, 7, 5, "ultima.regeneration.stamina_seconds"),
     InlineData(11, 7, double.NaN, 5, "ultima.regeneration.mana_seconds"),
     InlineData(11, 7, 7, 0, "ultima.regeneration.hunger_minutes"),
     InlineData(11, 7, 7, 1441, "ultima.regeneration.hunger_minutes")]
    public void Validate_AValueOutOfRange_NamesTheSetting(
        double hits, double stamina, double mana, int hungerMinutes, string setting
    )
    {
        var config = new RegenerationConfig
        {
            HitsSeconds = hits, StaminaSeconds = stamina, ManaSeconds = mana, HungerMinutes = hungerMinutes
        };

        Assert.Contains(setting, Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
