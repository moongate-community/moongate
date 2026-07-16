using Moongate.UO.Data.Bodies;

namespace Moongate.Tests.UO.Bodies;

public class BodyTests
{
    [Theory]
    [InlineData(400)]  // human male
    [InlineData(401)]  // human female
    [InlineData(605)]  // elf male
    [InlineData(606)]  // elf female
    [InlineData(402)]  // male ghost — still a humanoid body
    public void IsHumanoid_TrueForPlayerBodies(int value)
        => Assert.True(new Body(value).IsHumanoid);

    [Theory]
    [InlineData(0)]    // no body
    [InlineData(200)]  // horse
    [InlineData(1)]    // ogre
    public void IsHumanoid_FalseForCreatureBodies(int value)
        => Assert.False(new Body(value).IsHumanoid);
}
