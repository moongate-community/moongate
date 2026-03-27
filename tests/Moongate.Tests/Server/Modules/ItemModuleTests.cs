using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
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

    private sealed class ItemModuleTestSpatialWorldService : ISpatialWorldService
    {
        public List<(UOItemEntity Item, int MapId)> AddedOrUpdatedItems { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
            => AddedOrUpdatedItems.Add((item, mapId));

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => _ = mobile;

        public void AddRegion(JsonRegion region)
            => _ = region;

        public Task<int> BroadcastToPlayersAsync(
            Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = packet;
            _ = mapId;
            _ = location;
            _ = range;
            _ = excludeSessionId;

            return Task.FromResult(0);
        }

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        {
            _ = mapId;
            _ = centerSectorX;
            _ = centerSectorY;
            _ = radius;

            return [];
        }

        public int GetMusic(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return 0;
        }

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<Moongate.Server.Data.Session.GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            Moongate.Server.Data.Session.GameSession? excludeSession = null
        )
        {
            _ = location;
            _ = range;
            _ = mapId;
            _ = excludeSession;

            return [];
        }

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            return [];
        }

        public JsonRegion? GetRegionById(int regionId)
        {
            _ = regionId;

            return null;
        }

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return null;
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        {
            _ = item;
            _ = mapId;
            _ = oldLocation;
            _ = newLocation;
        }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
        {
            _ = mobile;
            _ = oldLocation;
            _ = newLocation;
        }

        public void RemoveEntity(Serial serial)
            => _ = serial;

        public JsonRegion? ResolveRegion(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return null;
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

    [Test]
    public void Spawn_WhenValidInput_ShouldAddItemToSpatialWorld()
    {
        var itemService = new ItemModuleTestItemService();
        var spatialWorldService = new ItemModuleTestSpatialWorldService();
        var module = new ItemModule(itemService, spatialWorldService);
        var position = CreatePositionTable(120, 210, 0, 1);

        var result = module.Spawn("gold", position);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(spatialWorldService.AddedOrUpdatedItems.Count, Is.EqualTo(1));
                Assert.That(spatialWorldService.AddedOrUpdatedItems[0].Item.Id, Is.EqualTo((Serial)1u));
                Assert.That(spatialWorldService.AddedOrUpdatedItems[0].MapId, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void SearchTemplates_WhenQueryMatchesTemplateIdPrefix_ShouldReturnStableItemMetadata()
    {
        var itemService = new ItemModuleTestItemService();
        var templateService = new ItemTemplateService();
        templateService.UpsertRange(
        [
            CreateTemplate("arrow", "Arrow", "0x0F3F"),
            CreateTemplate("arrow_bundle", "Arrow Bundle", "0x1BFB"),
            CreateTemplate("bone_armor", "Bone Armor", "0x144F")
        ]
        );
        var module = new ItemModule(itemService, itemTemplateService: templateService);

        var results = module.SearchTemplates("arr");

        Assert.Multiple(
            () =>
            {
                Assert.That(results.Length, Is.EqualTo(2));
                AssertResult(results, 1, "arrow", "Arrow", 0x0F3F);
                AssertResult(results, 2, "arrow_bundle", "Arrow Bundle", 0x1BFB);
            }
        );
    }

    [Test]
    public void SearchTemplates_WhenQueryMatchesDisplayNameSubstring_ShouldReturnSubstringMatches()
    {
        var itemService = new ItemModuleTestItemService();
        var templateService = new ItemTemplateService();
        templateService.UpsertRange(
        [
            CreateTemplate("ceremonial_dagger", "Ceremonial Blade", "0x0F52"),
            CreateTemplate("training_sword", "Training Blade", "0x13B9"),
            CreateTemplate("war_mace", "War Mace", "0x1407")
        ]
        );
        var module = new ItemModule(itemService, itemTemplateService: templateService);

        var results = module.SearchTemplates("blade");

        Assert.Multiple(
            () =>
            {
                Assert.That(results.Length, Is.EqualTo(2));
                AssertResult(results, 1, "ceremonial_dagger", "Ceremonial Blade", 0x0F52);
                AssertResult(results, 2, "training_sword", "Training Blade", 0x13B9);
            }
        );
    }

    [Test]
    public void SearchTemplates_WhenPageSizeExceedsMax_ShouldClampResultCount()
    {
        var itemService = new ItemModuleTestItemService();
        var templateService = new ItemTemplateService();

        for (var index = 1; index <= 60; index++)
        {
            templateService.Upsert(
                CreateTemplate(
                    $"search_item_{index:00}",
                    $"Search Item {index:00}",
                    "0x0EED"
                )
            );
        }

        var module = new ItemModule(itemService, itemTemplateService: templateService);

        var results = module.SearchTemplates("search_item", pageSize: 999);

        Assert.Multiple(
            () =>
            {
                Assert.That(results.Length, Is.EqualTo(50));
                AssertResult(results, 1, "search_item_01", "Search Item 01", 0x0EED);
                AssertResult(results, 50, "search_item_50", "Search Item 50", 0x0EED);
            }
        );
    }

    private static void AssertResult(Table results, int index, string templateId, string displayName, int itemId)
    {
        var entry = results.Get(index).Table;

        Assert.That(entry, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(entry!.Get("template_id").String, Is.EqualTo(templateId));
                Assert.That(entry.Get("display_name").String, Is.EqualTo(displayName));
                Assert.That((int)entry.Get("item_id").Number, Is.EqualTo(itemId));
            }
        );
    }

    private static ItemTemplateDefinition CreateTemplate(string id, string name, string itemId)
        => new()
        {
            Id = id,
            Name = name,
            Category = "Test",
            Description = name,
            ItemId = itemId,
            ScriptId = string.Empty
        };

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
