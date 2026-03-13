using Moongate.Server.Commands.WorldGen;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.Server.Data.Entities;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.WorldGen;

public sealed class CreateSpawnersCommandTests
{
    [Test]
    public async Task ExecuteCommandAsync_ShouldCreateSpawnerItemsWithSpawnerIdCustomProperty()
    {
        var spawnGuid = Guid.Parse("001a5320-820c-4300-96f9-676e428b55be");
        var seedDataService = new CreateSpawnersTestSeedDataService(
            [
                new(
                    0,
                    "Felucca",
                    "shared/felucca",
                    "Outdoors.json",
                    spawnGuid,
                    SpawnDefinitionKind.Spawner,
                    "Spawner (213)",
                    new(4066, 569, 0),
                    8,
                    TimeSpan.FromMinutes(20),
                    TimeSpan.FromMinutes(20),
                    0,
                    80,
                    80,
                    [new("PolarBear", 8, 100)]
                )
            ]
        );
        var itemService = new CreateSpawnersTestItemService();
        var command = new CreateSpawnersCommand(
            new CreateSpawnersTestEntityFactoryService(),
            seedDataService,
            itemService,
            new ImmediateBackgroundJobService()
        );
        var context = new CommandSystemContext(
            "create_spawners 0",
            ["0"],
            CommandSourceType.Console,
            0,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(itemService.CreatedItems, Has.Count.EqualTo(1));
        var item = itemService.CreatedItems[0];

        Assert.Multiple(
            () =>
            {
                Assert.That(item.MapId, Is.EqualTo(0));
                Assert.That(item.Location, Is.EqualTo(new Point3D(4066, 569, 0)));
                Assert.That(item.TryGetCustomString("spawner_id", out var spawnerId), Is.True);
                Assert.That(spawnerId, Is.EqualTo(spawnGuid.ToString("D")));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldSkipSpawner_WhenGuidIsEmpty()
    {
        var seedDataService = new CreateSpawnersTestSeedDataService(
            [
                new(
                    0,
                    "Felucca",
                    "shared/felucca",
                    "Outdoors.json",
                    Guid.Empty,
                    SpawnDefinitionKind.Spawner,
                    "Spawner (Invalid)",
                    new(4066, 569, 0),
                    1,
                    TimeSpan.FromMinutes(20),
                    TimeSpan.FromMinutes(20),
                    0,
                    10,
                    10,
                    [new("PolarBear", 1, 100)]
                )
            ]
        );
        var itemService = new CreateSpawnersTestItemService();
        var command = new CreateSpawnersCommand(
            new CreateSpawnersTestEntityFactoryService(),
            seedDataService,
            itemService,
            new ImmediateBackgroundJobService()
        );
        var context = new CommandSystemContext(
            "create_spawners 0",
            ["0"],
            CommandSourceType.Console,
            0,
            static (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(itemService.CreatedItems, Is.Empty);
    }

    private sealed class CreateSpawnersTestSeedDataService : ISeedDataService
    {
        private readonly IReadOnlyList<SpawnDefinitionEntry> _spawns;

        public CreateSpawnersTestSeedDataService(IReadOnlyList<SpawnDefinitionEntry> spawns)
        {
            _spawns = spawns;
        }

        public IReadOnlyList<DecorationEntry> GetDecorationsByMap(int mapId)
            => [];

        public IReadOnlyList<DoorComponentEntry> GetDoors()
            => [];

        public IReadOnlyList<WorldLocationEntry> GetLocations()
            => [];

        public IReadOnlyList<SignEntry> GetSignsByMap(int mapId)
            => [];

        public IReadOnlyList<SpawnDefinitionEntry> GetSpawnsByMap(int mapId)
            => [.._spawns.Where(spawn => spawn.MapId == mapId)];

        public IReadOnlyList<TeleporterEntry> GetTeleportersBySourceMap(int mapId)
            => [];
    }

    private sealed class CreateSpawnersTestEntityFactoryService : IEntityFactoryService
    {
        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
            => new()
            {
                Id = (Serial)0x400000A1u,
                Name = itemTemplateId,
                ItemId = 0x1F13,
                MapId = 0,
                Location = Point3D.Zero,
                ScriptId = "none",
                Direction = DirectionType.North
            };

        public UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null)
            => throw new NotSupportedException();

        public UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId)
            => throw new NotSupportedException();

        public UOItemEntity CreateStarterBackpack(Serial mobileId, StarterProfileContext profileContext)
            => throw new NotSupportedException();

        public UOItemEntity CreateStarterEquipment(Serial mobileId, ItemLayerType layer, StarterProfileContext profileContext)
            => throw new NotSupportedException();

        public UOItemEntity CreateStarterGold(
            Serial containerId,
            Point2D containerPosition,
            int quantity,
            StarterProfileContext profileContext
        )
            => throw new NotSupportedException();

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();
    }

    private sealed class CreateSpawnersTestItemService : IItemService
    {
        public List<UOItemEntity> CreatedItems { get; } = [];

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            CreatedItems.Add(item);

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(true);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(true);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class ImmediateBackgroundJobService : IBackgroundJobService
    {
        public void EnqueueBackground(Action job)
            => job();

        public void EnqueueBackground(Func<Task> job)
            => job().GetAwaiter().GetResult();

        public int ExecutePendingOnGameLoop(int maxActions = 100)
            => 0;

        public void PostToGameLoop(Action action)
            => action();

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            try
            {
                var result = backgroundJob();
                onGameLoopResult(result);
            }
            catch (Exception ex) when (onGameLoopError is not null)
            {
                onGameLoopError(ex);
            }
        }

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
        {
            try
            {
                var result = backgroundJob().GetAwaiter().GetResult();
                onGameLoopResult(result);
            }
            catch (Exception ex) when (onGameLoopError is not null)
            {
                onGameLoopError(ex);
            }
        }

        public void Start(int? workerCount = null) { }

        public Task StopAsync()
            => Task.CompletedTask;
    }
}
