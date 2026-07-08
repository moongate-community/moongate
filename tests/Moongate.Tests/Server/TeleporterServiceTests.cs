using Moongate.Server.Services;
using Moongate.UO.Data.Teleporters;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class TeleporterServiceTests
{
    private static TeleporterDefinition Tele(MapType map)
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
        service.Register(Tele(MapType.Felucca));
        service.Register(Tele(MapType.Malas));

        Assert.Equal(2, service.Count);
        Assert.Single(service.ForMap(MapType.Felucca));
        Assert.Equal(4, service.ForMap(MapType.Felucca)[0].Dst.X);
    }

    [Fact]
    public void ForMap_Unknown_IsEmpty()
    {
        var service = new TeleporterService();
        service.Register(Tele(MapType.Felucca));

        Assert.Empty(service.ForMap(MapType.Tokuno));
    }
}
