using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class CharactersConfigTests
{
    [Fact]
    public void Default_IsSevenAndValid()
    {
        var config = new CharactersConfig();

        config.Validate();
        Assert.Equal(7, config.MaxPerAccount);
    }

    [Theory, InlineData(1), InlineData(5), InlineData(6), InlineData(7)]
    public void Validate_SupportedSlotCounts_Pass(int max)
    {
        new CharactersConfig { MaxPerAccount = max }.Validate();
    }

    [Theory, InlineData(0), InlineData(2), InlineData(4), InlineData(8)]
    public void Validate_UnsupportedSlotCounts_Throw(int max)
    {
        var error = Assert.Throws<InvalidOperationException>(new CharactersConfig { MaxPerAccount = max }.Validate);

        Assert.Contains("characters.max_per_account", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletionDelay_DefaultsToADay()
    {
        Assert.Equal(24, new CharactersConfig().DeletionDelayHours);
    }

    [Theory, InlineData(0), InlineData(-1)]
    public void Validate_DeletionDelayBelowAnHour_Throws(int hours)
    {
        var error = Assert.Throws<InvalidOperationException>(new CharactersConfig { DeletionDelayHours = hours }.Validate);

        Assert.Contains("characters.deletion_delay_hours", error.Message, StringComparison.Ordinal);
    }
}
