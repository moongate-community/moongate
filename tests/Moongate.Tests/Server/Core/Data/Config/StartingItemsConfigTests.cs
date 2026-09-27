using Moongate.Server.Core.Data.Config;

namespace Moongate.Tests.Server.Core.Data.Config;

public sealed class StartingItemsConfigTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var config = new StartingItemsConfig();

        config.Validate();
        Assert.Equal(
            ("0x0e75_backpack", "0x0eed_gold_coin", 1000, 3),
            (config.BackpackTemplate, config.GoldTemplate, config.Gold, config.BestSkills)
        );
    }

    [Theory,
     InlineData("", "g", 0, 3),
     InlineData("b", " ", 0, 3),
     InlineData("b", "g", -1, 3),
     InlineData("b", "g", 0, 0)]
    public void Validate_BadValues_Throw(string backpack, string gold, int amount, int bestSkills)
    {
        var config = new StartingItemsConfig { BackpackTemplate = backpack, GoldTemplate = gold, Gold = amount, BestSkills = bestSkills };

        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
