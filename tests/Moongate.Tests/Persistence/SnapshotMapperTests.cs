using Moongate.Persistence.Data.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Persistence;

public class SnapshotMapperTests
{
    [Test]
    public void ToMobileSnapshot_ShouldPreserveEquippedItems_InLayerOrder()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x100u,
            Name = "test",
            Location = new(100, 200, 0),
            EquippedItemIds =
            {
                [ItemLayerType.Shirt] = (Serial)0x202u,
                [ItemLayerType.OneHanded] = (Serial)0x200u,
                [ItemLayerType.Shoes] = (Serial)0x201u
            }
        };

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);
        var restored = SnapshotMapper.ToMobileEntity(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.EquippedLayers.Length, Is.EqualTo(3));
            Assert.That(snapshot.EquippedItemIds.Length, Is.EqualTo(3));

            // Verify ordering by layer key (ascending)
            for (var i = 1; i < snapshot.EquippedLayers.Length; i++)
            {
                Assert.That(snapshot.EquippedLayers[i], Is.GreaterThanOrEqualTo(snapshot.EquippedLayers[i - 1]));
            }

            // Verify round-trip
            Assert.That(restored.EquippedItemIds.Count, Is.EqualTo(3));
            Assert.That(restored.EquippedItemIds[ItemLayerType.OneHanded], Is.EqualTo((Serial)0x200u));
            Assert.That(restored.EquippedItemIds[ItemLayerType.Shoes], Is.EqualTo((Serial)0x201u));
            Assert.That(restored.EquippedItemIds[ItemLayerType.Shirt], Is.EqualTo((Serial)0x202u));
        });
    }

    [Test]
    public void ToMobileSnapshot_WithEmptyEquipped_ShouldProduceEmptyArrays()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x101u,
            Name = "empty",
            Location = new(0, 0, 0)
        };

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.EquippedLayers, Is.Empty);
            Assert.That(snapshot.EquippedItemIds, Is.Empty);
        });
    }

    [Test]
    public void ToMobileSnapshot_ShouldPreserveCustomProperties()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x102u,
            Name = "props",
            Location = new(0, 0, 0)
        };
        entity.SetCustomProperty("test_key", new()
        {
            Type = ItemCustomPropertyType.Integer,
            IntegerValue = 42
        });

        var snapshot = SnapshotMapper.ToMobileSnapshot(entity);
        var restored = SnapshotMapper.ToMobileEntity(snapshot);

        Assert.That(restored.CustomProperties.Count, Is.EqualTo(1));
        Assert.That(restored.CustomProperties["test_key"].IntegerValue, Is.EqualTo(42));
    }
}
