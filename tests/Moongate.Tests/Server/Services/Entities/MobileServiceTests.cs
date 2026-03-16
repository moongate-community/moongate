using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.Entities;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Entities;

public class MobileServiceTests
{
    private sealed class TestItemFactoryService : IItemFactoryService
    {
        public Func<string, UOItemEntity> CreateItemFromTemplateImpl { get; init; } =
            itemTemplateId => new()
            {
                Id = (Serial)1u,
                Name = itemTemplateId,
                ItemId = 1
            };

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
            => CreateItemFromTemplateImpl(itemTemplateId);

        public UOItemEntity GetNewBackpack()
            => CreateItemFromTemplate("backpack");

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? template)
        {
            template = null;

            return false;
        }
    }

    private sealed class TestMobileFactoryService : IMobileFactoryService
    {
        public Func<string, Serial?, UOMobileEntity> CreateFromTemplateImpl { get; init; } =
            (_, _) => new();

        public UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null)
            => CreateFromTemplateImpl(mobileTemplateId, accountId);

        public UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId)
            => throw new NotSupportedException();
    }

    private sealed class TestMobileTemplateService : IMobileTemplateService
    {
        private readonly Dictionary<string, MobileTemplateDefinition> _definitions =
            new(StringComparer.OrdinalIgnoreCase);

        public int Count => _definitions.Count;

        public void Clear()
            => _definitions.Clear();

        public IReadOnlyList<MobileTemplateDefinition> GetAll()
            => _definitions.Values.ToList();

        public bool TryGet(string id, out MobileTemplateDefinition? definition)
        {
            definition = null;

            if (_definitions.TryGetValue(id, out var resolved))
            {
                definition = resolved;

                return true;
            }

            return false;
        }

        public void Upsert(MobileTemplateDefinition definition)
            => _definitions[definition.Id] = definition;

        public void UpsertRange(IEnumerable<MobileTemplateDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                Upsert(definition);
            }
        }
    }

    private sealed class TestLuaBrainRunner : ILuaBrainRunner
    {
        public List<(UOMobileEntity Mobile, string BrainId)> Registered { get; } = [];

        public List<Serial> Unregistered { get; } = [];

        public void EnqueueDeath(Serial mobileId, LuaBrainDeathContext deathContext)
        {
            _ = mobileId;
            _ = deathContext;
        }

        public void EnqueueInRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range = 3)
        {
            _ = listenerNpcId;
            _ = sourceMobile;
            _ = range;
        }

        public void EnqueueSpawn(MobileSpawnedFromSpawnerEvent gameEvent)
            => _ = gameEvent;

        public void EnqueueSpeech(SpeechHeardEvent gameEvent)
            => _ = gameEvent;

        public IReadOnlyList<LuaBrainContextMenuEntry> GetContextMenuEntries(
            UOMobileEntity mobile,
            UOMobileEntity? requester
        )
        {
            _ = mobile;
            _ = requester;

            return [];
        }

        public Task HandleAsync(SpeechHeardEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task HandleAsync(MobileAddedInWorldEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task HandleAsync(MobileSpawnedFromSpawnerEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public void Register(UOMobileEntity mobile, string brainId)
            => Registered.Add((mobile, brainId));

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public ValueTask TickAllAsync(long nowMilliseconds, CancellationToken cancellationToken = default)
        {
            _ = nowMilliseconds;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }

        public bool TryHandleContextMenuSelection(
            UOMobileEntity mobile,
            UOMobileEntity? requester,
            string menuKey,
            long sessionId
        )
        {
            _ = mobile;
            _ = requester;
            _ = menuKey;
            _ = sessionId;

            return false;
        }

        public void Unregister(Serial mobileId)
            => Unregistered.Add(mobileId);
    }

    private sealed class CountingPersistenceService : IPersistenceService
    {
        public CountingPersistenceService(IPersistenceUnitOfWork unitOfWork)
        {
            UnitOfWork = unitOfWork;
        }

        public IPersistenceUnitOfWork UnitOfWork { get; }

        public void Dispose() { }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class CountingPersistenceUnitOfWork : IPersistenceUnitOfWork
    {
        public CountingPersistenceUnitOfWork(
            IMobileRepository mobiles,
            IItemRepository items
        )
        {
            Mobiles = mobiles;
            Items = items;
            Accounts = new NullAccountRepository();
            BulletinBoardMessages = new NullBulletinBoardMessageRepository();
        }

        public IAccountRepository Accounts { get; }
        public IMobileRepository Mobiles { get; }
        public IItemRepository Items { get; }
        public IBulletinBoardMessageRepository BulletinBoardMessages { get; }

        public Serial AllocateNextAccountId()
            => (Serial)1u;

        public Serial AllocateNextItemId()
            => (Serial)1u;

        public Serial AllocateNextMobileId()
            => (Serial)1u;

        public ValueTask<CapturedWorldSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromException<CapturedWorldSnapshot>(new NotSupportedException());

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask SaveCapturedSnapshotAsync(
            CapturedWorldSnapshot capturedSnapshot,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromException(new NotSupportedException());

        public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());
    }

    private sealed class CountingMobileRepository : IMobileRepository
    {
        private readonly IReadOnlyList<UOMobileEntity> _mobiles;

        public CountingMobileRepository(IReadOnlyList<UOMobileEntity> mobiles)
        {
            _mobiles = mobiles;
        }

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_mobiles.Count);

        public ValueTask<IReadOnlyCollection<UOMobileEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOMobileEntity>>(_mobiles);

        public ValueTask<UOMobileEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_mobiles.FirstOrDefault(mobile => mobile.Id == id));

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOMobileEntity, bool> predicate,
            Func<UOMobileEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<TResult>>([.. _mobiles.Where(predicate).Select(selector)]);

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromException<bool>(new NotSupportedException());

        public ValueTask UpsertAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());
    }

    private sealed class CountingItemRepository : IItemRepository
    {
        private readonly IReadOnlyList<UOItemEntity> _items;

        public CountingItemRepository(IReadOnlyList<UOItemEntity> items)
        {
            _items = items;
        }

        public int QueryCallCount { get; private set; }
        public int GetByIdCallCount { get; private set; }

        public ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_items.Count);

        public ValueTask<IReadOnlyCollection<UOItemEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOItemEntity>>(_items);

        public ValueTask<UOItemEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;

            return ValueTask.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOItemEntity, bool> predicate,
            Func<UOItemEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
        {
            QueryCallCount++;

            return ValueTask.FromResult<IReadOnlyList<TResult>>([.. _items.Where(predicate).Select(selector)]);
        }

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromException<bool>(new NotSupportedException());

        public ValueTask UpsertAsync(UOItemEntity item, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());
    }

    private sealed class NullAccountRepository : IAccountRepository
    {
        public ValueTask<bool> AddAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => ValueTask.FromException<bool>(new NotSupportedException());

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(0);

        public ValueTask<bool> ExistsAsync(
            Func<UOAccountEntity, bool> predicate,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult(false);

        public ValueTask<IReadOnlyCollection<UOAccountEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<UOAccountEntity>>(Array.Empty<UOAccountEntity>());

        public ValueTask<UOAccountEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<UOAccountEntity?>(null);

        public ValueTask<UOAccountEntity?> GetByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<UOAccountEntity?>(null);

        public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
            Func<UOAccountEntity, bool> predicate,
            Func<UOAccountEntity, TResult> selector,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<TResult>>(Array.Empty<TResult>());

        public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
            => ValueTask.FromException<bool>(new NotSupportedException());

        public ValueTask UpsertAsync(UOAccountEntity account, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());
    }

    private sealed class NullBulletinBoardMessageRepository : IBulletinBoardMessageRepository
    {
        public ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyCollection<BulletinBoardMessageEntity>>(
                Array.Empty<BulletinBoardMessageEntity>()
            );

        public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(
            Serial boardId,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>(Array.Empty<BulletinBoardMessageEntity>());

        public ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(
            Serial messageId,
            CancellationToken cancellationToken = default
        )
            => ValueTask.FromResult<BulletinBoardMessageEntity?>(null);

        public ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default)
            => ValueTask.FromException<bool>(new NotSupportedException());

        public ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());
    }

    [Test]
    public async Task CreateOrUpdateAsync_ShouldAllocateSerial_WhenMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var factory = new TestMobileFactoryService();
        var itemFactory = new TestItemFactoryService();
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);
        var mobile = new UOMobileEntity
        {
            Id = Serial.Zero,
            Name = "spawned"
        };

        await service.CreateOrUpdateAsync(mobile);
        var saved = await persistence.UnitOfWork.Mobiles.GetByIdAsync(mobile.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Id, Is.Not.EqualTo(Serial.Zero));
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved!.Name, Is.EqualTo("spawned"));
            }
        );
    }

    [Test]
    public async Task DeleteAsync_ShouldRemovePersistedMobile()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var factory = new TestMobileFactoryService();
        var itemFactory = new TestItemFactoryService();
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);
        var id = persistence.UnitOfWork.AllocateNextMobileId();

        await persistence.UnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = id,
                Name = "delete-me"
            }
        );

        var deleted = await service.DeleteAsync(id);
        var reloaded = await persistence.UnitOfWork.Mobiles.GetByIdAsync(id);

        Assert.Multiple(
            () =>
            {
                Assert.That(deleted, Is.True);
                Assert.That(reloaded, Is.Null);
                Assert.That(luaBrainRunner.Unregistered, Has.Count.EqualTo(1));
                Assert.That(luaBrainRunner.Unregistered[0], Is.EqualTo(id));
            }
        );
    }

    [Test]
    public async Task GetAsync_ShouldReturnPersistedMobile()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var factory = new TestMobileFactoryService();
        var itemFactory = new TestItemFactoryService();
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);
        var id = persistence.UnitOfWork.AllocateNextMobileId();

        await persistence.UnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = id,
                Name = "npc-one"
            }
        );

        var mobile = await service.GetAsync(id);

        Assert.That(mobile, Is.Not.Null);
        Assert.That(mobile!.Name, Is.EqualTo("npc-one"));
    }

    [Test]
    public async Task GetPersistentMobilesInSectorAsync_ShouldHydrateEquippedItemReferences()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var factory = new TestMobileFactoryService();
        var itemFactory = new TestItemFactoryService();
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);

        var mobileId = persistence.UnitOfWork.AllocateNextMobileId();
        var equippedItemId = persistence.UnitOfWork.AllocateNextItemId();

        await persistence.UnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = mobileId,
                IsPlayer = false,
                MapId = 1,
                Location = new(130, 130, 0),
                EquippedItemIds = new()
                {
                    [ItemLayerType.Shirt] = equippedItemId
                }
            }
        );
        await persistence.UnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = equippedItemId,
                ItemId = 0x1517,
                Hue = 0x0444,
                EquippedMobileId = mobileId,
                EquippedLayer = ItemLayerType.Shirt
            }
        );

        const int locationX = 130;
        const int locationY = 130;
        var sectorX = locationX >> MapSectorConsts.SectorShift;
        var sectorY = locationY >> MapSectorConsts.SectorShift;

        var loaded = await service.GetPersistentMobilesInSectorAsync(1, sectorX, sectorY);
        var mobile = loaded.Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(loaded, Has.Count.EqualTo(1));
                Assert.That(mobile.EquippedItemIds.ContainsKey(ItemLayerType.Shirt), Is.True);
                Assert.That(mobile.EquippedItemReferences.ContainsKey(ItemLayerType.Shirt), Is.True);
            }
        );
    }

    [Test]
    public async Task GetPersistentMobilesInSectorAsync_ShouldHydrateSectorEquipmentWithSingleBulkItemQuery()
    {
        var firstMobileId = (Serial)0x00000011u;
        var secondMobileId = (Serial)0x00000012u;
        var firstEquippedItemId = (Serial)0x00001011u;
        var secondEquippedItemId = (Serial)0x00001012u;
        var mobiles = new List<UOMobileEntity>
        {
            new()
            {
                Id = firstMobileId,
                IsPlayer = false,
                MapId = 1,
                Location = new(130, 130, 0),
                EquippedItemIds = new()
                {
                    [ItemLayerType.Shirt] = firstEquippedItemId
                }
            },
            new()
            {
                Id = secondMobileId,
                IsPlayer = false,
                MapId = 1,
                Location = new(131, 131, 0),
                EquippedItemIds = new()
                {
                    [ItemLayerType.Pants] = secondEquippedItemId
                }
            }
        };
        var items = new List<UOItemEntity>
        {
            new()
            {
                Id = firstEquippedItemId,
                ItemId = 0x1517,
                EquippedMobileId = firstMobileId,
                EquippedLayer = ItemLayerType.Shirt
            },
            new()
            {
                Id = secondEquippedItemId,
                ItemId = 0x152E,
                EquippedMobileId = secondMobileId,
                EquippedLayer = ItemLayerType.Pants
            }
        };
        var itemRepository = new CountingItemRepository(items);
        var mobileRepository = new CountingMobileRepository(mobiles);
        var persistence =
            new CountingPersistenceService(new CountingPersistenceUnitOfWork(mobileRepository, itemRepository));
        var factory = new TestMobileFactoryService();
        var itemFactory = new TestItemFactoryService();
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);
        var sectorX = 130 >> MapSectorConsts.SectorShift;
        var sectorY = 130 >> MapSectorConsts.SectorShift;

        var loaded = await service.GetPersistentMobilesInSectorAsync(1, sectorX, sectorY);

        Assert.Multiple(
            () =>
            {
                Assert.That(loaded, Has.Count.EqualTo(2));
                Assert.That(itemRepository.QueryCallCount, Is.EqualTo(1));
                Assert.That(itemRepository.GetByIdCallCount, Is.EqualTo(0));
                Assert.That(
                    loaded.All(mobile => mobile.EquippedItemReferences.Count == 1),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task SpawnFromTemplateAsync_ShouldUseFactoryAndPersistMobile()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var expectedId = persistence.UnitOfWork.AllocateNextMobileId();
        var nextItemId = 1u;
        var templateService = new TestMobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "orc",
                Brain = "orc_warrior",
                FixedEquipment =
                [
                    new()
                    {
                        ItemTemplateId = "shirt",
                        Layer = ItemLayerType.Shirt
                    }
                ]
            }
        );
        var luaBrainRunner = new TestLuaBrainRunner();
        var itemFactory = new TestItemFactoryService
        {
            CreateItemFromTemplateImpl = itemTemplateId => new()
            {
                Id = (Serial)nextItemId++,
                Name = itemTemplateId,
                ItemId = 1
            }
        };
        var factory = new TestMobileFactoryService
        {
            CreateFromTemplateImpl = (templateId, accountId) =>
                                         new()
                                         {
                                             Id = expectedId,
                                             Name = $"template:{templateId}",
                                             AccountId = accountId ?? Serial.Zero
                                         }
        };
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);

        var spawned = await service.SpawnFromTemplateAsync(
                          "orc",
                          new(100, 200, 7),
                          1,
                          (Serial)25
                      );
        var saved = await persistence.UnitOfWork.Mobiles.GetByIdAsync(expectedId);

        Assert.Multiple(
            () =>
            {
                Assert.That(spawned.Id, Is.EqualTo(expectedId));
                Assert.That(spawned.Location, Is.EqualTo(new Point3D(100, 200, 7)));
                Assert.That(spawned.MapId, Is.EqualTo(1));
                Assert.That(spawned.AccountId, Is.EqualTo((Serial)25));
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved!.Name, Is.EqualTo("template:orc"));
                Assert.That(saved.Location, Is.EqualTo(new Point3D(100, 200, 7)));
                Assert.That(saved.MapId, Is.EqualTo(1));
                Assert.That(spawned.EquippedItemIds.ContainsKey(ItemLayerType.Shirt), Is.True);
                Assert.That(spawned.BrainId, Is.EqualTo("orc_warrior"));
                Assert.That(saved.BrainId, Is.EqualTo("orc_warrior"));
                Assert.That(luaBrainRunner.Registered, Is.Empty);
            }
        );
    }

    [Test]
    public async Task TrySpawnFromTemplateAsync_ShouldReturnFalse_WhenTemplateDoesNotExist()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        var factory = new TestMobileFactoryService();
        var itemFactory = new TestItemFactoryService();
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);

        var result = await service.TrySpawnFromTemplateAsync("missing_template", new(10, 10, 0), 0);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Spawned, Is.False);
                Assert.That(result.Mobile, Is.Null);
            }
        );
    }

    [Test]
    public async Task TrySpawnFromTemplateAsync_ShouldReturnSpawnedMobile_WhenTemplateExists()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var expectedId = persistence.UnitOfWork.AllocateNextMobileId();
        var templateService = new TestMobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "orc",
                Brain = "orc_warrior"
            }
        );
        var luaBrainRunner = new TestLuaBrainRunner();
        var factory = new TestMobileFactoryService
        {
            CreateFromTemplateImpl = (templateId, accountId) =>
                                         new()
                                         {
                                             Id = expectedId,
                                             Name = $"template:{templateId}",
                                             AccountId = accountId ?? Serial.Zero
                                         }
        };
        var itemFactory = new TestItemFactoryService();
        IMobileService service = new MobileService(persistence, factory, itemFactory, templateService, luaBrainRunner);

        var result = await service.TrySpawnFromTemplateAsync("orc", new(42, 66, 0), 1);
        var persisted = await persistence.UnitOfWork.Mobiles.GetByIdAsync(expectedId);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Spawned, Is.True);
                Assert.That(result.Mobile, Is.Not.Null);
                Assert.That(result.Mobile!.Id, Is.EqualTo(expectedId));
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted!.Id, Is.EqualTo(expectedId));
            }
        );
    }

    private static async Task<PersistenceService> CreatePersistenceServiceAsync(string rootDirectory)
    {
        var directories = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());
        var persistence = new PersistenceService(
            directories,
            new TimerWheelService(
                new()
                {
                    TickDuration = TimeSpan.FromMilliseconds(250),
                    WheelSize = 512
                }
            ),
            new(),
            new NetworkServiceTestGameEventBusService()
        );
        await persistence.StartAsync();

        return persistence;
    }
}
