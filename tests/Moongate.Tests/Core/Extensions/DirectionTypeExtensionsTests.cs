using Moongate.Core.Extensions;
using Moongate.Core.Types;

namespace Moongate.Tests.Core.Extensions;

public class DirectionTypeExtensionsTests
{
    [Fact]
    public void IsRunning_And_StripRunning_Work()
    {
        var running = DirectionType.East | DirectionType.Running;

        Assert.True(running.IsRunning());
        Assert.False(DirectionType.East.IsRunning());
        Assert.Equal(DirectionType.East, running.StripRunning());
    }

    [Theory, InlineData(DirectionType.North, DirectionType.South),
     InlineData(DirectionType.NorthEast, DirectionType.SouthWest), InlineData(DirectionType.West, DirectionType.East)]
    public void Opposite_Direction_ReturnsReverse(DirectionType direction, DirectionType expected)
        => Assert.Equal(expected, direction.Opposite());

    [Fact]
    public void Opposite_PreservesRunningFlag()
        => Assert.Equal(
            DirectionType.South | DirectionType.Running,
            (DirectionType.North | DirectionType.Running).Opposite()
        );

    [Theory, InlineData(DirectionType.North, 0, -1), InlineData(DirectionType.NorthEast, 1, -1),
     InlineData(DirectionType.East, 1, 0), InlineData(DirectionType.SouthEast, 1, 1), InlineData(DirectionType.South, 0, 1),
     InlineData(DirectionType.SouthWest, -1, 1), InlineData(DirectionType.West, -1, 0),
     InlineData(DirectionType.NorthWest, -1, -1)]
    public void ToOffset_EveryDirection_ReturnsUoScreenDelta(DirectionType direction, int dx, int dy)
        => Assert.Equal((dx, dy), direction.ToOffset());

    [Fact]
    public void ToOffset_RunningFlagSet_IsIgnored()
        => Assert.Equal((0, -1), (DirectionType.North | DirectionType.Running).ToOffset());
}
