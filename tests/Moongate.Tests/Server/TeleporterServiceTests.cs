using Moongate.Server.Services;
using Moongate.UO.Data.Teleporters;

namespace Moongate.Tests.Server;

public class TeleporterServiceTests
{
    private static TeleporterDefinition Tele(string map)
    {
        return new TeleporterDefinition
        {
            Src = new TeleporterEndpoint { Map = map, X = 1, Y = 2, Z = 3 },
            Dst = new TeleporterEndpoint { Map = map, X = 4, Y = 5, Z = 6 }
        };
    }

    [Fact]
    public void Register_CountsAll_AndFiltersBySourceMap()
    {
        var service = new TeleporterService();
        service.Register(Tele("Felucca"));
        service.Register(Tele("Malas"));

        Assert.Equal(2, service.Count);
        Assert.Single(service.ForMap("felucca"));
        Assert.Equal(4, service.ForMap("Felucca")[0].Dst.X);
    }

    [Fact]
    public void ForMap_Unknown_IsEmpty()
    {
        var service = new TeleporterService();
        service.Register(Tele("Felucca"));

        Assert.Empty(service.ForMap("Tokuno"));
    }
}
