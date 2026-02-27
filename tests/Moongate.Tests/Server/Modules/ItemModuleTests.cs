using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Modules;

public class ItemModuleTests
{
    private sealed class ItemModuleTestItemService : IItemService
    {
        public UOItemEntity? ItemToReturn { get; set; }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
        {
            _ = generateNewSerial;

            return item;
        }

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
        {
            _ = itemId;
            _ = generateNewSerial;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            _ = item;

            return Task.FromResult((Serial)1u);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            _ = itemTemplateId;

            return Task.FromResult(new UOItemEntity { Id = (Serial)1u });
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            _ = itemId;

            return Task.FromResult(true);
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            _ = itemId;
            _ = location;
            _ = mapId;

            return Task.FromResult<DropItemToGroundResult?>(null);
        }

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
        {
            _ = itemId;
            _ = mobileId;
            _ = layer;

            return Task.FromResult(true);
        }

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            return Task.FromResult(new List<UOItemEntity>());
        }

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
        {
            _ = itemId;

            return Task.FromResult(ItemToReturn);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((ItemToReturn is not null, ItemToReturn));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
        {
            _ = containerId;

            return Task.FromResult(new List<UOItemEntity>());
        }

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            _ = itemId;
            _ = containerId;
            _ = position;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            _ = itemId;
            _ = location;
            _ = mapId;

            return Task.FromResult(true);
        }

        public Task UpsertItemAsync(UOItemEntity item)
        {
            _ = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            _ = items;

            return Task.CompletedTask;
        }
    }

    [Test]
    public void Get_WhenItemDoesNotExist_ShouldReturnNull()
    {
        var itemService = new ItemModuleTestItemService();
        var module = new ItemModule(itemService);

        var reference = module.Get(0x301);

        Assert.That(reference, Is.Null);
    }

    [Test]
    public void Get_WhenItemExists_ShouldReturnLuaItemRef()
    {
        var itemService = new ItemModuleTestItemService
        {
            ItemToReturn = new()
            {
                Id = (Serial)0x300,
                Name = "Gold",
                MapId = 1,
                Location = new(120, 210, 0),
                Amount = 100,
                ItemId = 0x0EED,
                Hue = 0
            }
        };
        var module = new ItemModule(itemService);

        var reference = module.Get(0x300);

        Assert.Multiple(
            () =>
            {
                Assert.That(reference, Is.Not.Null);
                Assert.That(reference!.Serial, Is.EqualTo(0x300));
                Assert.That(reference.Name, Is.EqualTo("Gold"));
                Assert.That(reference.MapId, Is.EqualTo(1));
                Assert.That(reference.LocationX, Is.EqualTo(120));
                Assert.That(reference.LocationY, Is.EqualTo(210));
                Assert.That(reference.LocationZ, Is.EqualTo(0));
                Assert.That(reference.Amount, Is.EqualTo(100));
                Assert.That(reference.ItemId, Is.EqualTo(0x0EED));
                Assert.That(reference.Hue, Is.EqualTo(0));
            }
        );
    }
}
