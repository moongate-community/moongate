using Moongate.Server.Ultima.Data.Titles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services.Titles;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services.Titles;

public sealed class FameKarmaTitleServiceTests
{
    [Theory]
    [InlineData(-101, -101, "The Outcast")]
    [InlineData(0, -100, "The Outcast")]
    [InlineData(999, -1, "The Outcast")]
    [InlineData(0, 0, "")]
    [InlineData(1000, -100, "The Dread Lord")]
    [InlineData(1000, 0, "The Glorious Lord")]
    [InlineData(int.MinValue, int.MinValue, "The Outcast")]
    [InlineData(int.MaxValue, int.MaxValue, "The Glorious Lord")]
    public void GetTitle_SelectsInclusiveBandsAndClampsOutsideTheTable(int fame, int karma, string expected)
    {
        var service = CreateService();

        Assert.Equal(expected, service.GetTitle(fame, karma, GenderType.Male));
    }

    [Fact]
    public void GetTitle_FemaleUsesOverrideWhenPresent()
    {
        var service = CreateService();

        Assert.Equal("The Dread Lady", service.GetTitle(1000, -100, GenderType.Female));
    }

    [Fact]
    public void GetTitle_FemaleFallsBackToBaseTitleWhenOverrideIsMissing()
    {
        var service = CreateService();

        Assert.Equal("The Outcast", service.GetTitle(0, -100, GenderType.Female));
    }

    [Fact]
    public void GetTitle_ExplicitEmptyFemaleOverrideSuppressesPrefix()
    {
        var rows = new[] { new FameKarmaTitle(0, 0, "The Honored", "") };
        var service = new FameKarmaTitleService(new StubDataLoaderService().With(rows));

        Assert.Equal("", service.GetTitle(0, 0, GenderType.Female));
        Assert.Equal("The Honored", service.GetTitle(0, 0, GenderType.Male));
    }

    [Fact]
    public void GetTitle_MobileReadsCurrentScoresWithoutChangingCustomTitle()
    {
        var service = CreateService();
        var mobile = new MobileEntity { Fame = 0, Karma = -100, Gender = GenderType.Male, Title = "the Weaponsmith" };

        Assert.Equal("The Outcast", service.GetTitle(mobile));

        mobile.Fame = 1000;
        mobile.Karma = 0;

        Assert.Equal("The Glorious Lord", service.GetTitle(mobile));
        Assert.Equal("the Weaponsmith", mobile.Title);
    }

    private static FameKarmaTitleService CreateService()
    {
        var rows = new[]
        {
            new FameKarmaTitle(0, -100, "The Outcast"),
            new FameKarmaTitle(0, 0, ""),
            new FameKarmaTitle(1000, -100, "The Dread Lord", "The Dread Lady"),
            new FameKarmaTitle(1000, 0, "The Glorious Lord", "The Glorious Lady")
        };

        return new FameKarmaTitleService(new StubDataLoaderService().With(rows));
    }
}
