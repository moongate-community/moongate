using Moongate.Server.Core.Data.Config;

namespace Moongate.Tests.Server.Core.Data.Config;

public sealed class LineOfSightConfigTests
{
    [Fact]
    public void MaxDistance_Default_Is25()
    {
        Assert.Equal(25, new LineOfSightConfig().MaxDistance);
    }

    [Theory, InlineData(1), InlineData(25), InlineData(255)]
    public void Validate_DistanceInRange_Passes(int distance)
    {
        new LineOfSightConfig { MaxDistance = distance }.Validate();
    }

    [Theory, InlineData(0), InlineData(-1), InlineData(256)]
    public void Validate_DistanceOutOfRange_Throws(int distance)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new LineOfSightConfig { MaxDistance = distance }.Validate());

        Assert.Contains("max_distance", exception.Message);
    }
}
