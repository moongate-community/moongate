using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public class ItemModuleTests
{
    private sealed class ItemModuleTestItemService : IItemService
    {
        public UOItemEntity? ItemToReturn { get; set; }
        public string? LastSpawnTemplateId { get; private set; }
        public Serial LastMoveItemId { get; private set; }
        public Point3D LastMoveLocation { get; private set; }
        public int LastMoveMapId { get; private set; }
        public bool MoveItemToWorldResult { get; set; } = true;
        public UOItemEntity? LastUpsertedItem { get; private set; }

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

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

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            _ = itemId;

            return Task.FromResult(true);
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
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
            _ = sessionId;

            LastMoveItemId = itemId;
            LastMoveLocation = location;
            LastMoveMapId = mapId;

            return Task.FromResult(MoveItemToWorldResult);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            LastSpawnTemplateId = itemTemplateId;

            return Task.FromResult(
                new UOItemEntity
                {
                    Id = (Serial)1u,
                    ItemId = 0x0EED,
                    Name = "Spawned Item",
                    MapId = 0,
                    Location = Point3D.Zero,
                    Amount = 1
                }
            );
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((ItemToReturn is not null, ItemToReturn));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            LastUpsertedItem = item;

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
    public void Get_WhenItemExists_ShouldReturnLuaItemProxy()
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
                Hue = 0,
                ScriptId = "items.gold",
                ParentContainerId = (Serial)0x400,
                ContainerPosition = new(11, 22)
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
                Assert.That(reference.ScriptId, Is.EqualTo("items.gold"));
                Assert.That(reference.ParentContainerId, Is.EqualTo(0x400));
                Assert.That(reference.ContainerX, Is.EqualTo(11));
                Assert.That(reference.ContainerY, Is.EqualTo(22));
            }
        );
    }

    [Test]
    public void Spawn_WhenAmountIsZero_ShouldReturnNull()
    {
        var itemService = new ItemModuleTestItemService();
        var module = new ItemModule(itemService);
        var position = CreatePositionTable(120, 210, 0, 1);

        var result = module.Spawn("gold", position, 0);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Spawn_WhenMoveToWorldFails_ShouldReturnNull()
    {
        var itemService = new ItemModuleTestItemService
        {
            MoveItemToWorldResult = false
        };
        var module = new ItemModule(itemService);
        var position = CreatePositionTable(120, 210, 0, 1);

        var result = module.Spawn("gold", position);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Spawn_WhenPositionIsInvalid_ShouldReturnNull()
    {
        var itemService = new ItemModuleTestItemService();
        var module = new ItemModule(itemService);
        var invalidPosition = new Table(new())
        {
            ["x"] = 100,
            ["y"] = 200
        };

        var result = module.Spawn("gold", invalidPosition);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Spawn_WhenTemplateIdIsEmpty_ShouldReturnNull()
    {
        var itemService = new ItemModuleTestItemService();
        var module = new ItemModule(itemService);
        var position = CreatePositionTable(120, 210, 0, 1);

        var result = module.Spawn("", position);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Spawn_WhenValidInput_ShouldSpawnAndReturnLuaItemProxy()
    {
        var itemService = new ItemModuleTestItemService();
        var module = new ItemModule(itemService);
        var position = CreatePositionTable(120, 210, 0, 1);

        var result = module.Spawn("gold", position, 25);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(itemService.LastSpawnTemplateId, Is.EqualTo("gold"));
                Assert.That(itemService.LastMoveItemId, Is.EqualTo((Serial)1u));
                Assert.That(itemService.LastMoveLocation, Is.EqualTo(new Point3D(120, 210, 0)));
                Assert.That(itemService.LastMoveMapId, Is.EqualTo(1));
                Assert.That(itemService.LastUpsertedItem, Is.Not.Null);
                Assert.That(itemService.LastUpsertedItem!.Amount, Is.EqualTo(25));
            }
        );
    }

    private static Table CreatePositionTable(int x, int y, int z, int mapId)
    {
        var table = new Table(new())
        {
            ["x"] = x,
            ["y"] = y,
            ["z"] = z,
            ["map_id"] = mapId
        };

        return table;
    }
}
