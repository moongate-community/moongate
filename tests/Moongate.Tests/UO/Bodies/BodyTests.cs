using Moongate.UO.Data.Bodies;

namespace Moongate.Tests.UO.Bodies;

public class BodyTests
{
    [Theory, InlineData(0), InlineData(200), InlineData(1)]

    // no body
    // horse
     // ogre
    public void IsHumanoid_FalseForCreatureBodies(int value)
        => Assert.False(new Body(value).IsHumanoid);

    [Theory, InlineData(400), InlineData(401), InlineData(605), InlineData(606), InlineData(402)]

    // human male
    // human female
    // elf male
    // elf female
     // male ghost — still a humanoid body
    public void IsHumanoid_TrueForPlayerBodies(int value)
        => Assert.True(new Body(value).IsHumanoid);
}
