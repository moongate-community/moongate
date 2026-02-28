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
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Data.Events.Speech;

namespace Moongate.Tests.Server.Services.Entities;

public class MobileServiceTests
{
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

        public Task HandleAsync(SpeechHeardEvent gameEvent, CancellationToken cancellationToken = default)
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
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, templateService, luaBrainRunner);
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
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, templateService, luaBrainRunner);
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
        var templateService = new TestMobileTemplateService();
        var luaBrainRunner = new TestLuaBrainRunner();
        IMobileService service = new MobileService(persistence, factory, templateService, luaBrainRunner);
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
        IMobileService service = new MobileService(persistence, factory, templateService, luaBrainRunner);

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
                Assert.That(luaBrainRunner.Registered, Has.Count.EqualTo(1));
                Assert.That(luaBrainRunner.Registered[0].Mobile.Id, Is.EqualTo(expectedId));
                Assert.That(luaBrainRunner.Registered[0].BrainId, Is.EqualTo("orc_warrior"));
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
