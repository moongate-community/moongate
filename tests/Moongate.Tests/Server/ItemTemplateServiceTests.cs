using Moongate.Server.Services.Items;

namespace Moongate.Tests.Server;

public class ItemTemplateServiceTests
{
    [Fact]
    public void All_ReflectsRegistrations()
    {
        var service = new ItemTemplateService();
        service.Register(new() { Id = "a" });
        service.Register(new() { Id = "b" });

        Assert.Equal(2, service.All.Count);
    }

    [Fact]
    public void Register_ThenGetById_IsCaseInsensitive()
    {
        var service = new ItemTemplateService();
        service.Register(new() { Id = "broadsword", Name = "Broadsword" });

        Assert.Equal(1, service.Count);
        Assert.Equal("Broadsword", service.GetById("BROADSWORD")!.Name);
        Assert.Null(service.GetById("nope"));
    }
}
