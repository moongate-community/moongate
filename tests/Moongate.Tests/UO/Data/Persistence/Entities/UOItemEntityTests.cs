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
    public void CombatStats_And_Modifiers_ShouldBeAssignable()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000002,
            ItemId = 0x13B9,
            CombatStats = new()
            {
                MinStrength = 40,
                DamageMin = 11,
                DamageMax = 13
            },
            Modifiers = new()
            {
                StrengthBonus = 5,
                PhysicalResist = 10,
                Luck = 100
            }
        };

        Assert.Multiple(
            () =>
            {
                Assert.That(item.CombatStats, Is.Not.Null);
                Assert.That(item.CombatStats!.MinStrength, Is.EqualTo(40));
                Assert.That(item.CombatStats.DamageMin, Is.EqualTo(11));
                Assert.That(item.CombatStats.DamageMax, Is.EqualTo(13));
                Assert.That(item.Modifiers, Is.Not.Null);
                Assert.That(item.Modifiers!.StrengthBonus, Is.EqualTo(5));
                Assert.That(item.Modifiers.PhysicalResist, Is.EqualTo(10));
                Assert.That(item.Modifiers.Luck, Is.EqualTo(100));
            }
        );
    }

    [Test]
    public void CombatStats_And_Modifiers_ShouldDefaultToNull()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000001,
            ItemId = 0x0EED
        };

        Assert.Multiple(
            () =>
            {
                Assert.That(item.CombatStats, Is.Null);
                Assert.That(item.Modifiers, Is.Null);
            }
        );
    }

    [Test]
    public void CustomProperties_ShouldStoreTypedValues()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000310,
            ItemId = 0x0EED
        };

        item.SetCustomInteger("uses_left", 12);
        item.SetCustomBoolean("blessed", true);
        item.SetCustomDouble("scale", 1.25d);
        item.SetCustomString("sign_text", "Tommy's home");

        var hasInteger = item.TryGetCustomInteger("uses_left", out var integerValue);
        var hasBoolean = item.TryGetCustomBoolean("blessed", out var booleanValue);
        var hasDouble = item.TryGetCustomDouble("scale", out var doubleValue);
        var hasString = item.TryGetCustomString("sign_text", out var stringValue);
        var wrongType = item.TryGetCustomInteger("sign_text", out _);

        Assert.Multiple(
            () =>
            {
                Assert.That(hasInteger, Is.True);
                Assert.That(integerValue, Is.EqualTo(12));
                Assert.That(hasBoolean, Is.True);
                Assert.That(booleanValue, Is.True);
                Assert.That(hasDouble, Is.True);
                Assert.That(doubleValue, Is.EqualTo(1.25d));
                Assert.That(hasString, Is.True);
                Assert.That(stringValue, Is.EqualTo("Tommy's home"));
                Assert.That(wrongType, Is.False);
                Assert.That(item.CustomProperties, Has.Count.EqualTo(4));
            }
        );
    }

    [Test]
    public void GetHashCode_ShouldChange_WhenCustomPropertyChanges()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000401,
            ItemId = 0x0EED
        };

        item.SetCustomString("label_number", "1000");
        var firstHash = item.GetHashCode();
        item.SetCustomString("label_number", "1001");
        var secondHash = item.GetHashCode();

        Assert.That(secondHash, Is.Not.EqualTo(firstHash));
    }

    [Test]
    public void GetHashCode_ShouldChange_WhenRelevantStateChanges()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000400,
            ItemId = 0x0EED,
            Name = "Gold",
            Amount = 10,
            Hue = 0
        };

        var firstHash = item.GetHashCode();
        item.Amount = 20;
        var secondHash = item.GetHashCode();

        Assert.That(secondHash, Is.Not.EqualTo(firstHash));
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
    public void TotalWeight_ShouldReduceContainedWeight_WhenContainerIsQuiver()
    {
        var quiver = new UOItemEntity
        {
            Id = (Serial)0x40000140,
            ItemId = 0x2FB7,
            Weight = 2,
            IsQuiver = true,
            QuiverWeightReduction = 30
        };
        var arrows = new UOItemEntity
        {
            Id = (Serial)0x40000240,
            ItemId = 0x0F3F,
            Weight = 1,
            Amount = 10
        };

        quiver.AddItem(arrows, new(1, 1));

        Assert.That(quiver.TotalWeight, Is.EqualTo(9));
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
