using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server;

public class HeldItemOriginTests
{
    [Fact]
    public void From_ContainedItem_RecordsTheContainerAndSlot()
    {
        var item = new ItemEntity
        {
            Id = (Serial)1,
            ParentContainerId = (Serial)99,
            ContainerPosition = new(44, 65)
        };

        var origin = HeldItemOrigin.From(item);

        Assert.Equal((Serial)99, origin.ContainerId);
        Assert.Equal(new Point2D(44, 65), origin.ContainerPosition);
        Assert.Equal(Serial.Zero, origin.EquippedMobileId);
    }

    [Fact]
    public void From_GroundItem_RecordsTheMapAndPosition()
    {
        var item = new ItemEntity { Id = (Serial)1, MapId = 1, Position = new(100, 200, 5) };

        var origin = HeldItemOrigin.From(item);

        Assert.Equal(Serial.Zero, origin.ContainerId);
        Assert.Equal(Serial.Zero, origin.EquippedMobileId);
        Assert.Equal(1, origin.MapId);
        Assert.Equal(new Point3D(100, 200, 5), origin.WorldPosition);
    }

    [Fact]
    public void From_WornItem_RecordsTheMobileAndLayer()
    {
        var item = new ItemEntity
        {
            Id = (Serial)1,
            EquippedMobileId = (Serial)7,
            EquippedLayer = LayerType.Shirt
        };

        var origin = HeldItemOrigin.From(item);

        Assert.Equal((Serial)7, origin.EquippedMobileId);
        Assert.Equal(LayerType.Shirt, origin.EquippedLayer);
        Assert.Equal(Serial.Zero, origin.ContainerId);
    }
}
