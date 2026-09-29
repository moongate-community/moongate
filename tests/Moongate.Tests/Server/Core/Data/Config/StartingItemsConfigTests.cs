using Moongate.Server.Core.Data.Config;

namespace Moongate.Tests.Server.Core.Data.Config;

public sealed class StartingItemsConfigTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var config = new StartingItemsConfig();

        config.Validate();
        Assert.Equal(3, config.BestSkills);
    }

    [Theory, InlineData(0), InlineData(-1)]
    public void Validate_BadBestSkills_Throws(int bestSkills)
    {
        var config = new StartingItemsConfig { BestSkills = bestSkills };

        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
