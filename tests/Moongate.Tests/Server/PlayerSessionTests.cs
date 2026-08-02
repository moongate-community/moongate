using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Tests.Server;

public class PlayerSessionTests
{
    [Theory, InlineData(0, 5), InlineData(4, 5), InlineData(5, 5), InlineData(12, 12), InlineData(18, 18),
     InlineData(19, 18), InlineData(255, 18)]

    // below the minimum, including the client's "unset" 0
    // the minimum itself
    // inside the range, untouched
    // the maximum itself
     // above the maximum, including a modified client's nonsense
    public void ClampViewRange_HoldsTheRequestBetweenTheBounds(int requested, int expected)
        => Assert.Equal(expected, PlayerSession.ClampViewRange(requested));

    [Fact]
    public void SessionStateType_Defaults_StartAtAwaitingSeed()
    {
        Assert.Equal((byte)0, (byte)SessionStateType.AwaitingSeed);
        Assert.Equal((byte)1, (byte)SessionStateType.Login);
        Assert.Equal((byte)2, (byte)SessionStateType.Authenticated);
    }

    [Fact]
    public void ViewRangeBounds_AreTheClassicFiveToEighteen()
    {
        Assert.Equal(5, PlayerSession.MinViewRange);
        Assert.Equal(18, PlayerSession.MaxViewRange);
    }
}
