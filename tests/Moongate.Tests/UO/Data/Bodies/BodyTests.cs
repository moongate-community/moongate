using Moongate.UO.Data.Bodies;

namespace Moongate.Tests.UO.Data.Bodies;

public class BodyTests
{
    [Fact]
    public void Equality_AndConversions_RoundTrip()
    {
        var body = new Body(400);

        Assert.Equal(new(400), body);
        Assert.NotEqual(new(401), body);
        Assert.True(new Body(400) == body);
        Assert.Equal(400, body);
        Assert.Equal(body, (Body)400);
    }

    [Fact]
    public void GhostBody_IsGhost()
    {
        Assert.True(new Body(970).IsGhost);
        Assert.False(new Body(970).IsMale);
        Assert.False(new Body(970).IsFemale);
    }

    [Fact]
    public void HumanFemaleBody_IsFemaleOnly()
    {
        var body = new Body(401);

        Assert.True(body.IsFemale);
        Assert.False(body.IsMale);
        Assert.False(body.IsGhost);
    }

    [Fact]
    public void HumanMaleBody_IsMaleOnly()
    {
        var body = new Body(400);

        Assert.True(body.IsMale);
        Assert.False(body.IsFemale);
        Assert.False(body.IsGhost);
    }
}
