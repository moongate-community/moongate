using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.UO.Data.Persistence.Entities;

public class UOItemEntityTests
{
    [Test]
    public void AddItem_ShouldSetParentContainerAndPosition()
    {
        var container = new UOItemEntity
        {
            Id = (Serial)0x40000100,
            ItemId = 0x0E75
        };

        var item = new UOItemEntity
        {
            Id = (Serial)0x40000200,
            ItemId = 0x1515
        };

        container.AddItem(item, new(12, 34));

        Assert.Multiple(
            () =>
            {
                Assert.That(item.ParentContainerId, Is.EqualTo(container.Id));
                Assert.That(item.ContainerPosition.X, Is.EqualTo(12));
                Assert.That(item.ContainerPosition.Y, Is.EqualTo(34));
                Assert.That(container.Items.Count, Is.EqualTo(1));
                Assert.That(container.Items[0].Location, Is.EqualTo(new Point3D(12, 34, 0)));
                Assert.That(container.ContainedItemIds, Has.Count.EqualTo(1));
                Assert.That(container.ContainedItemIds[0], Is.EqualTo(item.Id));
                Assert.That(container.ContainedItemReferences.ContainsKey(item.Id), Is.True);
            }
        );
    }

    [Test]
    public void HydrateContainedItemsRuntime_ShouldRebuildChildrenAndReferences()
    {
        var container = new UOItemEntity
        {
            Id = (Serial)0x40000130,
            ItemId = 0x0E75
        };
        var child = new UOItemEntity
        {
            Id = (Serial)0x40000230,
            ItemId = 0x0EED,
            Hue = 7,
            ParentContainerId = container.Id,
            ContainerPosition = new(9, 9)
        };

        container.HydrateContainedItemsRuntime([child]);

        Assert.Multiple(
            () =>
            {
                Assert.That(container.Items.Count, Is.EqualTo(1));
                Assert.That(container.ContainedItemIds.Count, Is.EqualTo(1));
                Assert.That(container.ContainedItemIds[0], Is.EqualTo(child.Id));
                Assert.That(container.ContainedItemReferences.ContainsKey(child.Id), Is.True);
                Assert.That(container.ContainedItemReferences[child.Id].ItemId, Is.EqualTo(0x0EED));
            }
        );
    }

    [Test]
    public void UpdateItemLocation_ShouldReturnFalse_WhenItemIsNotContained()
    {
        var container = new UOItemEntity
        {
            Id = (Serial)0x40000120,
            ItemId = 0x0E75
        };

        var item = new UOItemEntity
        {
            Id = (Serial)0x40000220,
            ItemId = 0x1515
        };

        var updated = container.UpdateItemLocation(item, new(1, 2));

        Assert.Multiple(
            () =>
            {
                Assert.That(updated, Is.False);
                Assert.That(container.Items, Is.Empty);
                Assert.That(container.ContainedItemIds, Is.Empty);
            }
        );
    }

    [Test]
    public void UpdateItemLocation_ShouldUpdateContainedItemPosition()
    {
        var container = new UOItemEntity
        {
            Id = (Serial)0x40000110,
            ItemId = 0x0E75
        };

        var item = new UOItemEntity
        {
            Id = (Serial)0x40000210,
            ItemId = 0x1515,
            EquippedMobileId = (Serial)0x00000099,
            EquippedLayer = ItemLayerType.Shirt
        };

        container.AddItem(item, new(10, 20));
        var updated = container.UpdateItemLocation(item, new(40, 50));

        Assert.Multiple(
            () =>
            {
                Assert.That(updated, Is.True);
                Assert.That(item.ParentContainerId, Is.EqualTo(container.Id));
                Assert.That(item.ContainerPosition.X, Is.EqualTo(40));
                Assert.That(item.ContainerPosition.Y, Is.EqualTo(50));
                Assert.That(item.EquippedMobileId, Is.EqualTo(Serial.Zero));
                Assert.That(item.EquippedLayer, Is.Null);
                Assert.That(container.Items[0].Location, Is.EqualTo(new Point3D(40, 50, 0)));
                Assert.That(container.ContainedItemReferences[item.Id].Id, Is.EqualTo(item.Id));
            }
        );
    }
}
