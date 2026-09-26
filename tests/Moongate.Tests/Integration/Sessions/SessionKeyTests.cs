using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Tests.Integration.Sessions;

public sealed class SessionKeyTests
{
    [Fact]
    public void Constructor_BlankName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SessionKey<int>(" "));
        Assert.Throws<ArgumentNullException>(() => new SessionKey<int>(null!));
    }

    [Fact]
    public void Constructor_WithoutDefault_UsesTheDefaultOfTheType()
    {
        Assert.Equal(0, new SessionKey<int>("Score").Default);
        Assert.Null(new SessionKey<string?>("Title").Default);
        Assert.Equal(7, new SessionKey<int>("Score", 7).Default);
    }

    [Fact]
    public void ToString_ReturnsTheName()
    {
        Assert.Equal("Score", new SessionKey<int>("Score").ToString());
    }
}
