using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class PetsConfigTests
{
    [Fact]
    public void Defaults_AreFiveFollowers()
    {
        var config = new PetsConfig();

        Assert.Equal(5, config.MaxFollowers);
        config.Validate();
    }

    [Theory, InlineData(0), InlineData(51), InlineData(-1)]
    public void Validate_AValueOutOfRange_NamesTheSetting(int followers)
    {
        var config = new PetsConfig { MaxFollowers = followers };

        Assert.Contains("ultima.pets.max_followers", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(1), InlineData(50)]
    public void Validate_TheLimits_AreAccepted(int followers)
    {
        new PetsConfig { MaxFollowers = followers }.Validate();
    }

    [Fact]
    public void UltimaConfig_HasThePetsSection_AndValidatesIt()
    {
        var config = new UltimaConfig();

        Assert.Equal(5, config.Pets.MaxFollowers);

        config.Pets.MaxFollowers = 0;

        Assert.Contains("ultima.pets.max_followers", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
