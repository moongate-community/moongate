using Moongate.Server.Services;
using Moongate.UO.Data.Containers;

namespace Moongate.Tests.Server;

public class ContainerGumpServiceTests
{
    [Fact]
    public void Register_IndexesByGumpIdAndItemId()
    {
        var service = new ContainerGumpService();
        service.Register(new ContainerGumpLayout { GumpId = 9, DropSound = 66, ItemIds = [8198, 3786] });

        Assert.Equal(1, service.Count);
        Assert.Equal(66, service.GetByGumpId(9)!.DropSound);
        Assert.Equal(9, service.GetByItemId(3786)!.GumpId);
        Assert.Null(service.GetByGumpId(999));
        Assert.Null(service.GetByItemId(999));
    }

    [Fact]
    public void All_IsOrderedByGumpId()
    {
        var service = new ContainerGumpService();
        service.Register(new ContainerGumpLayout { GumpId = 61 });
        service.Register(new ContainerGumpLayout { GumpId = 9 });

        Assert.Equal(new[] { 9, 61 }, service.All.Select(l => l.GumpId).ToArray());
    }
}
