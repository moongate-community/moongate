using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class WorldConfigTests
{
    [Fact]
    public void ViewRange_Default_Is18()
    {
        Assert.Equal(18, new WorldConfig().ViewRange);
    }

    [Theory, InlineData(5), InlineData(18), InlineData(24)]
    public void Validate_RangeTheClientSupports_Passes(int range)
    {
        new WorldConfig { ViewRange = range }.Validate();
    }

    [Theory, InlineData(4), InlineData(25), InlineData(0)]
    public void Validate_RangeTheClientDoesNotSupport_Throws(int range)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new WorldConfig { ViewRange = range }.Validate());

        Assert.Contains("world.view_range", exception.Message);
    }
}
