using Moongate.Core.Primitives;

namespace Moongate.Tests.Core.Primitives;

public sealed class BodyTests
{
    [Theory, InlineData(400), InlineData(402), InlineData(605), InlineData(666), InlineData(694)]
    public void IsMale_MaleBodiesAliveOrDead(int id)
    {
        var body = new Body((ushort)id);

        Assert.True(body.IsMale);
        Assert.False(body.IsFemale);
    }

    [Theory, InlineData(401), InlineData(403), InlineData(606), InlineData(667), InlineData(695), InlineData(1253)]
    public void IsFemale_FemaleBodiesAliveOrDead(int id)
    {
        var body = new Body((ushort)id);

        Assert.True(body.IsFemale);
        Assert.False(body.IsMale);
    }

    [Theory, InlineData(402, true), InlineData(608, true), InlineData(970, true), InlineData(400, false), InlineData(9, false)]
    public void IsGhost_OnlyGhostBodies(int id, bool expected)
    {
        Assert.Equal(expected, new Body((ushort)id).IsGhost);
    }

    [Theory, InlineData(666, true), InlineData(695, true), InlineData(400, false)]
    public void IsGargoyle_OnlyGargoyleBodies(int id, bool expected)
    {
        Assert.Equal(expected, new Body((ushort)id).IsGargoyle);
    }

    [Fact]
    public void Equality_And_ToString()
    {
        Assert.Equal(new Body(400), new Body(400));
        Assert.True(new Body(400) != new Body(401));
        Assert.Equal("0x0190", new Body(400).ToString());
    }
}
