using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class StableConfigTests
{
    [Fact]
    public void Defaults_Are10PetsAnd30Gold()
    {
        var config = new StableConfig();

        Assert.Equal((10, 30), (config.MaxPets, config.Fee));
        config.Validate();
    }

    [Theory,
     InlineData(0, 30, "ultima.stable.max_pets"),
     InlineData(51, 30, "ultima.stable.max_pets"),
     InlineData(10, -1, "ultima.stable.fee"),
     InlineData(10, 100001, "ultima.stable.fee")]
    public void Validate_AValueOutOfRange_NamesTheSetting(int pets, int fee, string setting)
    {
        var config = new StableConfig { MaxPets = pets, Fee = fee };

        Assert.Contains(setting, Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(1, 0), InlineData(50, 100000)]
    public void Validate_TheLimits_AreAccepted(int pets, int fee)
    {
        new StableConfig { MaxPets = pets, Fee = fee }.Validate();
    }

    [Fact]
    public void UltimaConfig_HasTheStableSection_AndValidatesIt()
    {
        var config = new UltimaConfig();

        Assert.Equal(10, config.Stable.MaxPets);

        config.Stable.MaxPets = 0;

        Assert.Contains("ultima.stable.max_pets", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
