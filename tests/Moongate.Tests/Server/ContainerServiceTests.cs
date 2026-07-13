using Moongate.Server.Services.Items;
using Moongate.UO.Data.Containers;

namespace Moongate.Tests.Server;

public class ContainerServiceTests
{
    [Fact]
    public void Register_ThenGetById_IsCaseInsensitive()
    {
        var service = new ContainerService();
        service.Register(new ContainerDefinition { Id = "backpack", ItemId = 3701, Width = 7, Height = 4, Name = "Backpack" });

        Assert.Equal(1, service.Count);
        Assert.Equal(7, service.GetById("BACKPACK")!.Width);
        Assert.Null(service.GetById("nope"));
    }

    [Fact]
    public void All_IsOrderedById()
    {
        var service = new ContainerService();
        service.Register(new ContainerDefinition { Id = "bag" });
        service.Register(new ContainerDefinition { Id = "armoire" });

        Assert.Equal(new[] { "armoire", "bag" }, service.All.Select(c => c.Id).ToArray());
    }
}
