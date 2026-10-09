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

    [Fact]
    public void Defaults_AreAnHourlyDrainOfTenAndTheModernUORates()
    {
        var config = new PetsConfig();

        Assert.Equal((60, 10, 10, 1, 3), (config.LoyaltyDrainMinutes, config.LoyaltyDrain, config.FoodGain, config.ObeyGain, config.DisobeyLoss));
    }

    [Theory,
     InlineData("loyalty_drain_minutes", 0), InlineData("loyalty_drain_minutes", 1441),
     InlineData("loyalty_drain", 0), InlineData("loyalty_drain", 101),
     InlineData("food_gain", 0), InlineData("obey_gain", -1), InlineData("disobey_loss", 101)]
    public void Validate_ALoyaltyValueOutOfRange_NamesTheSetting(string name, int value)
    {
        var config = new PetsConfig();

        switch (name)
        {
            case "loyalty_drain_minutes": config.LoyaltyDrainMinutes = value; break;
            case "loyalty_drain": config.LoyaltyDrain = value; break;
            case "food_gain": config.FoodGain = value; break;
            case "obey_gain": config.ObeyGain = value; break;
            default: config.DisobeyLoss = value; break;
        }

        Assert.Contains("ultima.pets." + name, Assert.Throws<InvalidOperationException>(config.Validate).Message);
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
