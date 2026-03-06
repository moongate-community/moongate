using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Interfaces.Services.Entities;
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
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;

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

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? template)
        {
            template = null;

            return false;
        }

        public UOItemEntity GetNewBackpack()
            => CreateItemFromTemplate("backpack");
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

        public void EnqueueSpeech(SpeechHeardEvent gameEvent)
            => _ = gameEvent;

        public void EnqueueDeath(Serial mobileId, LuaBrainDeathContext deathContext)
        {
            _ = mobileId;
            _ = deathContext;
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

        public void Unregister(Serial mobileId)
            => Unregistered.Add(mobileId);
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

        var loaded = await service.GetPersistentMobilesInSectorAsync(1, 4, 4);
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
