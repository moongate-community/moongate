using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Services.World;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.World;

public class DoorGeneratorBuilderTests
{
    [SetUp]
    public void SetUp()
    {
        TileData.ItemTable[0x000A] = new("east_frame", UOTileFlag.None, 0, 0, 0, 0, 0, 0);
        TileData.ItemTable[0x000C] = new("west_frame", UOTileFlag.None, 0, 0, 0, 0, 0, 0);
        TileData.ItemTable[0x2000] = new(
            "blocked_surface",
            UOTileFlag.Surface | UOTileFlag.Impassable,
            0,
            0,
            0,
            0,
            0,
            20
        );
    }

    [Test]
    public void Name_ShouldBeDoors()
    {
        var service = CreateService([], 16, 16);

        Assert.That(service.Name, Is.EqualTo("doors"));
    }

    [Test]
    public async Task GenerateAsync_ShouldStorePlacementWithFacing_ForSingleDoorPattern()
    {
        var staticTiles = new Dictionary<(int X, int Y), List<StaticTile>>
        {
            [(0, 0)] = [new(0x000C, 0)],
            [(2, 0)] = [new(0x000A, 0)]
        };
        var service = CreateService(staticTiles, 16, 16);
        var logs = new List<string>();

        await service.GenerateAsync(logs.Add);

        Assert.Multiple(
            () =>
            {
                Assert.That(service.LastGeneratedDoorCount, Is.EqualTo(1));
                Assert.That(service.LastGeneratedDoors, Has.Count.EqualTo(1));
                Assert.That(service.LastGeneratedDoors[0].MapId, Is.EqualTo(1));
                Assert.That(service.LastGeneratedDoors[0].Location, Is.EqualTo(new Point3D(1, 0, 0)));
                Assert.That(service.LastGeneratedDoors[0].Facing, Is.EqualTo(DoorGenerationFacing.WestCW));
                Assert.That(logs.Any(static line => line.Contains("Door generation started")), Is.True);
                Assert.That(logs.Any(static line => line.Contains("Map 1: 1 door candidates")), Is.True);
            }
        );
    }

    [Test]
    public async Task GenerateAsync_ShouldStoreTwoPlacements_ForDoubleDoorPattern()
    {
        var staticTiles = new Dictionary<(int X, int Y), List<StaticTile>>
        {
            [(0, 0)] = [new(0x000C, 0)],
            [(3, 0)] = [new(0x000A, 0)]
        };
        var service = CreateService(staticTiles, 16, 16);

        await service.GenerateAsync();

        Assert.That(service.LastGeneratedDoors, Has.Count.EqualTo(2));
        Assert.Multiple(
            () =>
            {
                Assert.That(service.LastGeneratedDoors[0].Location, Is.EqualTo(new Point3D(1, 0, 0)));
                Assert.That(service.LastGeneratedDoors[0].Facing, Is.EqualTo(DoorGenerationFacing.WestCW));
                Assert.That(service.LastGeneratedDoors[1].Location, Is.EqualTo(new Point3D(2, 0, 0)));
                Assert.That(service.LastGeneratedDoors[1].Facing, Is.EqualTo(DoorGenerationFacing.EastCCW));
            }
        );
    }

    [Test]
    public async Task GenerateAsync_ShouldSkipDoor_WhenCanFitFailsAtPlacementLocation()
    {
        var staticTiles = new Dictionary<(int X, int Y), List<StaticTile>>
        {
            [(0, 0)] = [new(0x000C, 0)],
            [(2, 0)] = [new(0x000A, 0)],
            [(1, 0)] = [new(0x2000, 0)]
        };
        var service = CreateService(staticTiles, 16, 16);

        await service.GenerateAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.LastGeneratedDoorCount, Is.EqualTo(0));
                Assert.That(service.LastGeneratedDoors, Is.Empty);
            }
        );
    }

    private static DoorGeneratorBuilder CreateService(
        Dictionary<(int X, int Y), List<StaticTile>> staticTiles,
        int width,
        int height
    )
    {
        var tileQuery = new FakeMovementTileQueryService(staticTiles, width, height);
        var spatial = new RegionDataLoaderTestSpatialWorldService();
        var itemService = new FakeDoorGeneratorItemService();
        var specs = new List<DoorGenerationMapSpec>
        {
            new(1, [new Rectangle2D(0, 0, 4, 4)])
        };

        return new(tileQuery, spatial, itemService, specs);
    }

    private sealed class FakeDoorGeneratorItemService : IItemService
    {
        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(
                new UOItemEntity
                {
                    Id = (Serial)0x40000001,
                    ItemId = 0x0675,
                    Location = Point3D.Zero
                }
            );

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class FakeMovementTileQueryService : IMovementTileQueryService
    {
        private readonly Dictionary<(int X, int Y), List<StaticTile>> _staticTiles;
        private readonly int _width;
        private readonly int _height;

        public FakeMovementTileQueryService(
            Dictionary<(int X, int Y), List<StaticTile>> staticTiles,
            int width,
            int height
        )
        {
            _staticTiles = staticTiles;
            _width = width;
            _height = height;
        }

        public bool TryGetMapBounds(int mapId, out int width, out int height)
        {
            width = _width;
            height = _height;

            return true;
        }

        public bool TryGetLandTile(int mapId, int x, int y, out LandTile landTile)
        {
            landTile = default;

            return true;
        }

        public IReadOnlyList<StaticTile> GetStaticTiles(int mapId, int x, int y)
            => _staticTiles.TryGetValue((x, y), out var tiles) ? tiles : Array.Empty<StaticTile>();

        public bool CanFit(int mapId, int x, int y, int z, int height = 16)
            => !_staticTiles.TryGetValue((x, y), out var tiles) ||
               tiles.All(static tile => tile.ID != 0x2000);
    }
}
