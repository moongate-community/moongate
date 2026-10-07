using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class ItemsConfigTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var config = new ItemsConfig();

        config.Validate();
        Assert.Equal(("0x0e75_backpack", "0x0eed_gold_coin"), (config.BackpackTemplate, config.GoldTemplate));
    }

    [Theory, InlineData("", "g"), InlineData("b", " ")]
    public void Validate_AnEmptyTemplate_Throws(string backpack, string gold)
    {
        Assert.Throws<InvalidOperationException>(
            new ItemsConfig { BackpackTemplate = backpack, GoldTemplate = gold }.Validate
        );
    }
}
