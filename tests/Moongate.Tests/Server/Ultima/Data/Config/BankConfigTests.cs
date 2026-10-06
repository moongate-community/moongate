using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class BankConfigTests
{
    [Fact]
    public void Defaults_AreThoseOfModernUO()
    {
        var config = new BankConfig();

        Assert.Equal((125, 60000, 5000, 1_000_000), (config.MaxItems, config.MaxWithdraw, config.MinCheck, config.MaxCheck));
        config.Validate();
    }

    [Theory,
     InlineData(-1, 60000, 5000, 1_000_000, "ultima.bank.max_items"),
     InlineData(10001, 60000, 5000, 1_000_000, "ultima.bank.max_items"),
     InlineData(125, 0, 5000, 1_000_000, "ultima.bank.max_withdraw"),
     // A pile holds 60000: a withdrawal is one pile.
     InlineData(125, 60001, 5000, 1_000_000, "ultima.bank.max_withdraw"),
     InlineData(125, 60000, 0, 1_000_000, "ultima.bank.min_check"),
     InlineData(125, 60000, 5000, 4999, "ultima.bank.min_check"),
     InlineData(125, 60000, 5000, 2_000_000_001, "ultima.bank.max_check")]
    public void Validate_AValueOutOfRange_NamesTheSetting(
        int items, int withdraw, int minCheck, int maxCheck, string setting
    )
    {
        var config = new BankConfig { MaxItems = items, MaxWithdraw = withdraw, MinCheck = minCheck, MaxCheck = maxCheck };

        Assert.Contains(setting, Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(0, 1, 1, 1), InlineData(10000, 60000, 2_000_000_000, 2_000_000_000)]
    public void Validate_TheLimits_AreAccepted(int items, int withdraw, int minCheck, int maxCheck)
    {
        new BankConfig { MaxItems = items, MaxWithdraw = withdraw, MinCheck = minCheck, MaxCheck = maxCheck }.Validate();
    }

    [Fact]
    public void UltimaConfig_HasTheBankSection_AndValidatesIt()
    {
        var config = new UltimaConfig();

        Assert.Equal(125, config.Bank.MaxItems);

        config.Bank.MaxWithdraw = 0;

        Assert.Contains("ultima.bank.max_withdraw", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
