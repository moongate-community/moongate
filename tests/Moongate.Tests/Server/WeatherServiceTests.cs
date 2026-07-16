using Moongate.Server.Services.World;

namespace Moongate.Tests.Server;

public class WeatherServiceTests
{
    [Fact]
    public void All_IsOrderedById()
    {
        var service = new WeatherService();
        service.Register(new() { Id = 2, Name = "Tropical" });
        service.Register(new() { Id = 0, Name = "No Weather" });

        Assert.Equal(new[] { 0, 2 }, service.All.Select(w => w.Id).ToArray());
    }

    [Fact]
    public void Register_ThenGetById()
    {
        var service = new WeatherService();
        service.Register(new() { Id = 1, Name = "Desert", MaxTemp = 40 });

        Assert.Equal(1, service.Count);
        Assert.Equal("Desert", service.GetById(1)!.Name);
        Assert.Null(service.GetById(99));
    }
}
