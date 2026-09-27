using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ContainerLayoutServiceTests
{
    private readonly ContainerLayoutService _service = new(
        new StubDataLoaderService().With(
            new ContainerContent { Name = "default", Gump = 0x3C, Bounds = new(new Point2D(44, 65), new Point2D(186, 159)), Default = true },
            new ContainerContent { Name = "bag", Gump = 0x3D, Bounds = new(new Point2D(29, 34), new Point2D(137, 128)), Items = [0x0E76] }
        )
    );

    [Fact]
    public void GetLayout_ListedGraphic_OrTheDefault()
    {
        Assert.Equal("bag", _service.GetLayout(0x0E76).Name);
        Assert.Equal("default", _service.GetLayout(0x0E75).Name);
    }

    [Fact]
    public void RandomGridPosition_IsAlwaysInsideTheBounds()
    {
        Assert.All(
            Enumerable.Range(0, 500).Select(_ => _service.RandomGridPosition(0x0E76)),
            p => Assert.True(p.X is >= 29 and < 137 && p.Y is >= 34 and < 128, $"{p}")
        );
    }
}
