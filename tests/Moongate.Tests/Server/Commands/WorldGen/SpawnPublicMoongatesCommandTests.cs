using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Server.Commands.WorldGen;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.Tests.Server.Commands.WorldGen;

public sealed class SpawnPublicMoongatesCommandTests
{
    private sealed class SpawnPublicMoongatesTestDefinitionService : IPublicMoongateDefinitionService
    {
        public IReadOnlyList<PublicMoongateGroupDefinition> Groups { get; init; } = [];

        public IReadOnlyList<PublicMoongateGroupDefinition> Load()
            => Groups;
    }

    private sealed class SpawnPublicMoongatesTestBackgroundJobService : IBackgroundJobService
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
                onGameLoopResult(backgroundJob());
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
                onGameLoopResult(backgroundJob().GetAwaiter().GetResult());
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

    private sealed class SpawnPublicMoongatesTestPersistenceService : IPersistenceService
    {
        public SpawnPublicMoongatesTestPersistenceService(IReadOnlyCollection<UOItemEntity> items)
        {
            UnitOfWork = new SpawnPublicMoongatesTestPersistenceUnitOfWork(items);
        }

        public IPersistenceUnitOfWork UnitOfWork { get; }

        public void Dispose() { }

        public Task SaveAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class SpawnPublicMoongatesTestPersistenceUnitOfWork : IPersistenceUnitOfWork
    {
        private uint _nextItemId = 0x40000000;

        public SpawnPublicMoongatesTestPersistenceUnitOfWork(IReadOnlyCollection<UOItemEntity> items)
        {
            Items = new SpawnPublicMoongatesTestItemRepository(items);
            Accounts = new SpawnPublicMoongatesUnusedAccountRepository();
            Mobiles = new SpawnPublicMoongatesUnusedMobileRepository();
            BulletinBoardMessages = new SpawnPublicMoongatesUnusedBulletinBoardMessageRepository();
        }

        public IAccountRepository Accounts { get; }

        public IMobileRepository Mobiles { get; }

        public IItemRepository Items { get; }

        public IBulletinBoardMessageRepository BulletinBoardMessages { get; }

        public Serial AllocateNextAccountId()
            => throw new NotSupportedException();

        public Serial AllocateNextItemId()
            => (Serial)_nextItemId++;

        public Serial AllocateNextMobileId()
            => throw new NotSupportedException();

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class SpawnPublicMoongatesUnusedBulletinBoardMessageRepository : IBulletinBoardMessageRepository
    {
        public ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<BulletinBoardMessageEntity>>([]);

        public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(
            Serial boardId,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>([]);

        public ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(
            Serial messageId,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<BulletinBoardMessageEntity?>(null);

        public ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class SpawnPublicMoongatesTestItemRepository : IItemRepository
    {
        private readonly IReadOnlyCollection<UOItemEntity> _items;

        public SpawnPublicMoongatesTestItemRepository(IReadOnlyCollection<UOItemEntity> items)
        {
            _items = items;
        }

        public ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.Count);

        public ValueTask<IReadOnlyCollection<UOItemEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items);

        public ValueTask<UOItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.FirstOrDefault(item => item.Id == id));

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOItemEntity, bool> predicate,
            Func<UOItemEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<TResult>>([.. _items.Where(predicate).Select(selector)]);

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask UpsertAsync(UOItemEntity item, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class SpawnPublicMoongatesUnusedAccountRepository : IAccountRepository
    {
        public ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<bool> ExistsAsync(Func<UOAccountEntity, bool> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyCollection<UOAccountEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<UOAccountEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<UOAccountEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOAccountEntity, bool> predicate,
            Func<UOAccountEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask UpsertAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class SpawnPublicMoongatesUnusedMobileRepository : IMobileRepository
    {
        public ValueTask BulkUpsertAsync(IReadOnlyList<UOMobileEntity> mobiles, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyCollection<UOMobileEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<UOMobileEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOMobileEntity, bool> predicate,
            Func<UOMobileEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask UpsertAsync(UOMobileEntity item, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class SpawnPublicMoongatesTestItemFactoryService : IItemFactoryService
    {
        private uint _nextItemId = 0x50000000;

        public List<string> CreatedTemplateIds { get; } = [];

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        {
            CreatedTemplateIds.Add(itemTemplateId);

            var item = new UOItemEntity
            {
                Id = (Serial)_nextItemId++,
                Name = itemTemplateId,
                ItemId = 0x0F6C,
                ScriptId = "items.public_moongate"
            };
            item.SetCustomString("item_template_id", itemTemplateId);

            return item;
        }

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
        {
            if (string.Equals(itemTemplateId, "public_moongate", StringComparison.OrdinalIgnoreCase))
            {
                definition = new ItemTemplateDefinition { Id = "public_moongate" };

                return true;
            }

            definition = null;

            return false;
        }
    }

    private sealed class SpawnPublicMoongatesTestItemService : IItemService
    {
        public List<UOItemEntity> CreatedItems { get; } = [];

        public List<Serial> DeletedItems { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

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
        {
            DeletedItems.Add(itemId);

            return Task.FromResult(true);
        }

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<Moongate.Server.Data.Items.DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
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
    }

    private sealed class SpawnPublicMoongatesTestSpatialWorldService : ISpatialWorldService
    {
        public List<UOItemEntity> AddedItems { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            item.MapId = mapId;
            AddedItems.Add(item);
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile) { }

        public void AddRegion(JsonRegion region) { }

        public Task<int> BroadcastToPlayersAsync(
            Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
            => Task.FromResult(0);

        public List<Moongate.UO.Data.Maps.MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

        public List<Moongate.Server.Data.Session.GameSession> GetPlayersInRange(Point3D location, int range, int mapId, Moongate.Server.Data.Session.GameSession? excludeSession = null)
            => [];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => [];

        public JsonRegion? GetRegionById(int regionId)
            => null;

        public Moongate.UO.Data.Maps.MapSector? GetSectorByLocation(int mapId, Point3D location)
            => null;

        public SectorSystemStats GetStats()
            => default;

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }
    }

    [Test]
    public async Task ExecuteCommandAsync_ShouldReplaceExistingPublicMoongatesFromSharedDataset()
    {
        var existingPublicMoongates = new[]
        {
            CreateWorldItem((Serial)0x00001001u, "public_moongate"),
            CreateWorldItem((Serial)0x00001002u, "public_moongate"),
            CreateWorldItem((Serial)0x00001003u, "moongate")
        };
        var persistence = new SpawnPublicMoongatesTestPersistenceService(existingPublicMoongates);
        var background = new SpawnPublicMoongatesTestBackgroundJobService();
        var definitions = new SpawnPublicMoongatesTestDefinitionService
        {
            Groups =
            [
                new PublicMoongateGroupDefinition(
                    "felucca",
                    "Felucca",
                    [
                        new PublicMoongateDestinationDefinition("moonglow", "Moonglow", 0, new Point3D(4467, 1283, 5)),
                        new PublicMoongateDestinationDefinition("britain", "Britain", 0, new Point3D(1336, 1997, 5))
                    ]
                ),
                new PublicMoongateGroupDefinition(
                    "ilshenar",
                    "Ilshenar",
                    [
                        new PublicMoongateDestinationDefinition("chaos", "Chaos", 2, new Point3D(1721, 218, 96))
                    ]
                )
            ]
        };
        var itemFactory = new SpawnPublicMoongatesTestItemFactoryService();
        var itemService = new SpawnPublicMoongatesTestItemService();
        var spatial = new SpawnPublicMoongatesTestSpatialWorldService();
        var output = new List<string>();
        var command = new SpawnPublicMoongatesCommand(
            persistence,
            background,
            definitions,
            itemFactory,
            itemService,
            spatial
        );
        var context = new CommandSystemContext(
            "spawn_public_moongates",
            [],
            CommandSourceType.Console,
            0,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.DeletedItems, Is.EquivalentTo(new[] { (Serial)0x00001001u, (Serial)0x00001002u }));
                Assert.That(itemService.CreatedItems, Has.Count.EqualTo(3));
                Assert.That(itemFactory.CreatedTemplateIds, Is.EqualTo(new[] { "public_moongate", "public_moongate", "public_moongate" }));
                Assert.That(itemService.CreatedItems.Select(static item => item.Location), Is.EquivalentTo(
                    new[]
                    {
                        new Point3D(4467, 1283, 5),
                        new Point3D(1336, 1997, 5),
                        new Point3D(1721, 218, 96)
                    }
                ));
                Assert.That(itemService.CreatedItems.Select(static item => item.MapId), Is.EqualTo(new[] { 0, 0, 2 }));
                Assert.That(spatial.AddedItems, Has.Count.EqualTo(3));
                Assert.That(output[^1], Does.Contain("removed 2"));
                Assert.That(output[^1], Does.Contain("spawned 3"));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenArgumentsAreProvided_ShouldPrintUsage()
    {
        var persistence = new SpawnPublicMoongatesTestPersistenceService([]);
        var background = new SpawnPublicMoongatesTestBackgroundJobService();
        var definitions = new SpawnPublicMoongatesTestDefinitionService();
        var itemFactory = new SpawnPublicMoongatesTestItemFactoryService();
        var itemService = new SpawnPublicMoongatesTestItemService();
        var spatial = new SpawnPublicMoongatesTestSpatialWorldService();
        var output = new List<string>();
        var command = new SpawnPublicMoongatesCommand(
            persistence,
            background,
            definitions,
            itemFactory,
            itemService,
            spatial
        );
        var context = new CommandSystemContext(
            "spawn_public_moongates felucca",
            ["felucca"],
            CommandSourceType.Console,
            0,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(output, Has.Count.EqualTo(1));
                Assert.That(output[0], Is.EqualTo("Usage: .spawn_public_moongates"));
                Assert.That(itemService.CreatedItems, Is.Empty);
                Assert.That(itemService.DeletedItems, Is.Empty);
            }
        );
    }

    private static UOItemEntity CreateWorldItem(Serial id, string templateId)
    {
        var item = new UOItemEntity
        {
            Id = id,
            MapId = 0,
            Location = new Point3D(100, 100, 0),
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero
        };
        item.SetCustomString("item_template_id", templateId);

        return item;
    }
}
