using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Items;

public sealed class DoorLockServiceTests
{
    private sealed class DoorLockServiceTestItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public DoorLockServiceTestItemService(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(false);

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<Moongate.Server.Data.Items.DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
            => Task.FromResult(false);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(_items.Values.Where(x => x.ParentContainerId == containerId).ToList());

        public Task<bool> MoveItemToContainerAsync(
            Serial itemId,
            Serial containerId,
            Point2D position,
            long sessionId = 0
        )
            => Task.FromResult(false);

        public Task<bool> MoveItemToWorldAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult(false);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult(
                _items.TryGetValue(itemId, out var item)
                    ? (true, item)
                    : (false, (UOItemEntity?)null)
            );

        public Task UpsertItemAsync(UOItemEntity item)
        {
            _items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class DoorLockServiceTestDoorDataService : IDoorDataService
    {
        public IReadOnlyList<Moongate.Server.Data.World.DoorComponentEntry> GetAllEntries()
            => [];

        public void SetEntries(IReadOnlyList<Moongate.Server.Data.World.DoorComponentEntry> entries) { }

        public bool TryGetToggleDefinition(int itemId, out Moongate.Server.Data.World.DoorToggleDefinition definition)
        {
            if (itemId is 0x0685 or 0x0686 or 0x0687 or 0x0688)
            {
                definition = new(itemId, itemId + 1, true, Point3D.Zero);

                return true;
            }

            definition = default;

            return false;
        }
    }

    [Test]
    public async Task LockDoorAsync_WhenUnlockedDoorSelected_ShouldAssignLockIdToDoorAndLinkedDoor()
    {
        var firstDoor = CreateDoor((Serial)0x40000001u);
        var secondDoor = CreateDoor((Serial)0x40000002u);
        firstDoor.SetCustomInteger(ItemCustomParamKeys.Door.LinkSerial, (uint)secondDoor.Id);
        secondDoor.SetCustomInteger(ItemCustomParamKeys.Door.LinkSerial, (uint)firstDoor.Id);

        var itemService = new DoorLockServiceTestItemService(firstDoor, secondDoor);
        var service = new DoorLockService(itemService, new DoorLockServiceTestDoorDataService());

        var result = await service.LockDoorAsync(firstDoor.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Locked, Is.True);
                Assert.That(result.LockId, Is.Not.Null.And.Not.Empty);
                Assert.That(firstDoor.TryGetCustomBoolean(ItemCustomParamKeys.Door.Locked, out var firstLocked) && firstLocked, Is.True);
                Assert.That(secondDoor.TryGetCustomBoolean(ItemCustomParamKeys.Door.Locked, out var secondLocked) && secondLocked, Is.True);
                Assert.That(firstDoor.TryGetCustomString(ItemCustomParamKeys.Door.LockId, out var firstLockId) ? firstLockId : null, Is.EqualTo(result.LockId));
                Assert.That(secondDoor.TryGetCustomString(ItemCustomParamKeys.Door.LockId, out var secondLockId) ? secondLockId : null, Is.EqualTo(result.LockId));
            }
        );
    }

    [Test]
    public async Task LockDoorAsync_WhenDoorAlreadyLocked_ShouldReturnExistingLockId()
    {
        var door = CreateDoor((Serial)0x40000001u);
        door.SetCustomBoolean(ItemCustomParamKeys.Door.Locked, true);
        door.SetCustomString(ItemCustomParamKeys.Door.LockId, "existing-lock");
        var itemService = new DoorLockServiceTestItemService(door);
        var service = new DoorLockService(itemService, new DoorLockServiceTestDoorDataService());

        var result = await service.LockDoorAsync(door.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Locked, Is.True);
                Assert.That(result.LockId, Is.EqualTo("existing-lock"));
            }
        );
    }

    [Test]
    public async Task UnlockDoorAsync_WhenDoorAndLinkLocked_ShouldClearLockMetadata()
    {
        var firstDoor = CreateDoor((Serial)0x40000001u);
        var secondDoor = CreateDoor((Serial)0x40000002u);
        firstDoor.SetCustomInteger(ItemCustomParamKeys.Door.LinkSerial, (uint)secondDoor.Id);
        secondDoor.SetCustomInteger(ItemCustomParamKeys.Door.LinkSerial, (uint)firstDoor.Id);
        firstDoor.SetCustomBoolean(ItemCustomParamKeys.Door.Locked, true);
        secondDoor.SetCustomBoolean(ItemCustomParamKeys.Door.Locked, true);
        firstDoor.SetCustomString(ItemCustomParamKeys.Door.LockId, "door-lock");
        secondDoor.SetCustomString(ItemCustomParamKeys.Door.LockId, "door-lock");
        var itemService = new DoorLockServiceTestItemService(firstDoor, secondDoor);
        var service = new DoorLockService(itemService, new DoorLockServiceTestDoorDataService());

        var result = await service.UnlockDoorAsync(firstDoor.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(firstDoor.TryGetCustomBoolean(ItemCustomParamKeys.Door.Locked, out _), Is.False);
                Assert.That(firstDoor.TryGetCustomString(ItemCustomParamKeys.Door.LockId, out _), Is.False);
                Assert.That(secondDoor.TryGetCustomBoolean(ItemCustomParamKeys.Door.Locked, out _), Is.False);
                Assert.That(secondDoor.TryGetCustomString(ItemCustomParamKeys.Door.LockId, out _), Is.False);
            }
        );
    }

    private static UOItemEntity CreateDoor(Serial id)
        => new()
        {
            Id = id,
            ItemId = 0x0685,
            MapId = 0,
            Location = new Point3D(100, 100, 0),
            Name = "door",
            ScriptId = "items.door"
        };
}
