using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Tests.Server.Ultima.Services.Internal;

public sealed class GhostBodiesTests
{
    [Theory]
    [InlineData(400, 402)]
    [InlineData(401, 403)]
    [InlineData(605, 607)]
    [InlineData(606, 608)]
    [InlineData(666, 694)]
    [InlineData(667, 695)]
    public void GhostOf_LivingBody_GivesItsGhost(int living, int ghost)
    {
        Assert.Equal(ghost, GhostBodies.GhostOf(living));
        Assert.True(GhostBodies.IsGhost(ghost));
        Assert.False(GhostBodies.IsGhost(living));
    }

    [Theory]
    [InlineData(402, 400)]
    [InlineData(403, 401)]
    [InlineData(607, 605)]
    [InlineData(608, 606)]
    [InlineData(694, 666)]
    [InlineData(695, 667)]
    public void LivingOf_GhostBody_GivesItsLivingBody(int ghost, int living)
    {
        Assert.Equal(living, GhostBodies.LivingOf(ghost));
    }

    [Fact]
    public void GhostOf_BodyWithoutGhost_StaysAsItIs()
    {
        Assert.Equal(0x11, GhostBodies.GhostOf(0x11));
        Assert.Equal(0x11, GhostBodies.LivingOf(0x11));
    }
}
