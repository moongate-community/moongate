using Moongate.Server.Services;
using Moongate.UO.Data.Titles;

namespace Moongate.Tests.Server;

public class TitleServiceTests
{
    private static TitleService BuildRealisticTable()
    {
        var service = new TitleService();
        service.Register(new FameTitleGroup
        {
            Fame = 1249,
            Karma = [new() { Karma = 624, Title = "{0}" }, new() { Karma = 10000, Title = "The Trustworthy {0}" }]
        });
        service.Register(new FameTitleGroup
        {
            Fame = 10000,
            Karma =
            [
                new() { Karma = -10000, Title = "The Dread {1} {0}" },
                new() { Karma = 624, Title = "{1} {0}" },
                new() { Karma = 10000, Title = "The Glorious {1} {0}" }
            ]
        });
        return service;
    }

    [Fact]
    public void GetTitle_NeutralLowFame_IsBareName()
    {
        var service = BuildRealisticTable();

        Assert.Equal("Bob", service.GetTitle("Bob", 0, 0, female: false));
    }

    [Fact]
    public void GetTitle_TopFameTopKarma_Female_UsesLady()
    {
        var service = BuildRealisticTable();

        Assert.Equal("The Glorious Lady Bob", service.GetTitle("Bob", 15000, 15000, female: true));
    }

    [Fact]
    public void GetTitle_TopFameBottomKarma_Male_UsesLord()
    {
        var service = BuildRealisticTable();

        Assert.Equal("The Dread Lord Bob", service.GetTitle("Bob", 15000, -10000, female: false));
    }

    [Fact]
    public void GetTitle_EmptyTable_ReturnsName()
    {
        var service = new TitleService();

        Assert.Equal("Bob", service.GetTitle("Bob", 5000, 5000, female: false));
    }
}
