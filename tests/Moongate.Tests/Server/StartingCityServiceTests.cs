using Moongate.Server.Services.World;
using Moongate.UO.Data.StartingCities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class StartingCityServiceTests
{
    [Fact]
    public void GetByIndex_OutOfRange_ReturnsNull()
    {
        var service = new StartingCityService();
        service.Register(City("New Haven"));

        Assert.Null(service.GetByIndex(-1));
        Assert.Null(service.GetByIndex(1));
    }

    [Fact]
    public void Register_PreservesOrder_AndGetByIndex()
    {
        var service = new StartingCityService();
        service.Register(City("New Haven"));
        service.Register(City("Britain"));

        Assert.Equal(2, service.Count);
        Assert.Equal("New Haven", service.GetByIndex(0)!.City);
        Assert.Equal("Britain", service.GetByIndex(1)!.City);
    }

    private static StartingCity City(string name)
        => new() { City = name, Building = "Inn", Description = 1, X = 1, Y = 2, Z = 3, Map = MapType.Trammel };
}
