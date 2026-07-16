using Moongate.Server.Services.World;
using Moongate.UO.Data.Signs;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class SignServiceTests
{
    [Fact]
    public void ForMap_Unknown_IsEmpty()
    {
        var service = new SignService();
        service.Register(Sign(MapType.Felucca, "#1016093"));

        Assert.Empty(service.ForMap(MapType.Tokuno));
    }

    [Fact]
    public void Register_CountsAll_AndFiltersByMap()
    {
        var service = new SignService();
        service.Register(Sign(MapType.Felucca, "#1016093"));
        service.Register(Sign(MapType.Malas, "Bank"));

        Assert.Equal(2, service.Count);
        Assert.Single(service.ForMap(MapType.Felucca));
        Assert.Equal("#1016093", service.ForMap(MapType.Felucca)[0].Label);
    }

    private static SignEntry Sign(MapType map, string label)
        => new() { Map = map, ItemId = 3032, X = 1, Y = 2, Z = 0, Label = label };
}
