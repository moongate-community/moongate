using Moongate.Server.Modules;
using Moongate.Tests.Server.Modules.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Modules;

public class MobileInventoryModuleTests
{
    [Test]
    public void ConsumeItem_WhenMatchingItemExistsInQuiver_ShouldConsumeQuiverBeforeBackpack()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x214,
            Name = "Ranger",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x610,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = mobile.Location
        };
        var quiver = new UOItemEntity
        {
            Id = (Serial)0x611,
            Name = "Quiver",
            ItemId = 0x1B02,
            MapId = 1,
            Location = mobile.Location,
            IsQuiver = true
        };
        var quiverArrow = new UOItemEntity
        {
            Id = (Serial)0x612,
            Name = "Arrow",
            ItemId = 0x0F3F,
            Amount = 3,
            IsStackable = true,
            MapId = 1,
            Location = mobile.Location
        };
        var backpackArrow = new UOItemEntity
        {
            Id = (Serial)0x613,
            Name = "Arrow",
            ItemId = 0x0F3F,
            Amount = 8,
            IsStackable = true,
            MapId = 1,
            Location = mobile.Location
        };
        quiver.AddItem(quiverArrow, new(1, 1));
        backpack.AddItem(backpackArrow, new(1, 1));
        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.AddEquippedItem(ItemLayerType.Cloak, quiver);
        mobile.BackpackId = backpack.Id;

        var itemService = new MobileModuleTestItemService();
        var module = new MobileInventoryModule(itemService);

        var consumed = module.ConsumeItem(mobile, 0x0F3F, 1);

        Assert.Multiple(
            () =>
            {
                Assert.That(consumed, Is.True);
                Assert.That(quiverArrow.Amount, Is.EqualTo(2));
                Assert.That(backpackArrow.Amount, Is.EqualTo(8));
            }
        );
    }

    [Test]
    public void AddItemToBackpack_WhenCharacterHasBackpack_ShouldSpawnAndMoveItem()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x215,
            Name = "Gatherer",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x620,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = mobile.Location
        };
        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.BackpackId = backpack.Id;

        var itemService = new MobileModuleTestItemService
        {
            SpawnedItem = new()
            {
                Id = (Serial)0x621,
                Name = "Arrow",
                ItemId = 0x0F3F,
                Amount = 1,
                IsStackable = true,
                MapId = 1,
                Location = Point3D.Zero
            }
        };
        var module = new MobileInventoryModule(itemService);

        var addedItem = module.AddItemToBackpack(mobile, "arrow", 5);

        Assert.Multiple(
            () =>
            {
                Assert.That(addedItem, Is.Not.Null);
                Assert.That(itemService.LastMoveItemId, Is.EqualTo((Serial)0x621));
                Assert.That(itemService.LastContainerId, Is.EqualTo((Serial)0x620));
                Assert.That(itemService.SpawnedItem!.Amount, Is.EqualTo(5));
            }
        );
    }
}
