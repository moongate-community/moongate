using Moongate.Server.Core.Data.Config;

namespace Moongate.Tests.Server.Core.Data.Config;

public sealed class StartingItemsConfigTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var config = new StartingItemsConfig();

        config.Validate();
        Assert.Equal((1000, 3), (config.Gold, config.BestSkills));
    }

    [Theory,
     InlineData(-1, 3),
     InlineData(0, 0),
     InlineData(65536, 3)]
    public void Validate_BadValues_Throw(int amount, int bestSkills)
    {
        var config = new StartingItemsConfig { Gold = amount, BestSkills = bestSkills };

        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
