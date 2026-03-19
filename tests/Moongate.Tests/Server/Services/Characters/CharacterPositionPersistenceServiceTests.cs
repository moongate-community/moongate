using System.Net.Sockets;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Services.Characters;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Characters;

public sealed class CharacterPositionPersistenceServiceTests
{
    [Test]
    public async Task HandleAsync_ShouldNotOverwritePersistedEquipmentFromStaleSessionCharacter()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);

        try
        {
            var characterId = (Serial)0x00000044u;
            var persisted = new UOMobileEntity
            {
                Id = characterId,
                Location = new(100, 100, 0),
                MapId = 1,
                EquippedItemIds = new()
                {
                    [ItemLayerType.Shoes] = (Serial)0x40000099u
                }
            };
            await persistence.UnitOfWork.Mobiles.UpsertAsync(persisted);

            var sessions = new FakeGameNetworkSessionService();
            var bus = new GameEventBusService();
            var service = new CharacterPositionPersistenceService(bus, sessions, persistence);
            var session = CreateSession(characterId, new(100, 100, 0), 1);

            // Simulate stale in-memory session character without equipment.
            session.Character!.EquippedItemIds.Clear();
            sessions.Add(session);

            await service.HandleAsync(
                new(
                    session.SessionId,
                    session.CharacterId,
                    1,
                    1,
                    new(100, 100, 0),
                    new(101, 100, 0)
                )
            );

            var reloaded = await persistence.UnitOfWork.Mobiles.GetByIdAsync(characterId);

            Assert.That(reloaded, Is.Not.Null);
            Assert.Multiple(
                () =>
                {
                    Assert.That(reloaded!.Location, Is.EqualTo(new Point3D(101, 100, 0)));
                    Assert.That(reloaded.EquippedItemIds.TryGetValue(ItemLayerType.Shoes, out var equippedId), Is.True);
                    Assert.That(equippedId, Is.EqualTo((Serial)0x40000099u));
                }
            );
        }
        finally
        {
            await persistence.StopAsync();
            persistence.Dispose();
        }
    }

    [Test]
    public async Task HandleAsync_ShouldPersistCharacterLocationFromRuntimeSession()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);

        try
        {
            var sessions = new FakeGameNetworkSessionService();
            var bus = new GameEventBusService();
            var service = new CharacterPositionPersistenceService(bus, sessions, persistence);
            var session = CreateSession((Serial)0x00000002u, new(100, 100, 0), 1);
            sessions.Add(session);

            await service.HandleAsync(
                new(
                    session.SessionId,
                    session.CharacterId,
                    1,
                    1,
                    new(100, 100, 0),
                    new(101, 100, 0)
                )
            );

            var persisted = await persistence.UnitOfWork.Mobiles.GetByIdAsync(session.CharacterId);

            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted!.Location, Is.EqualTo(new Point3D(101, 100, 0)));
            Assert.That(persisted.MapId, Is.EqualTo(1));
        }
        finally
        {
            await persistence.StopAsync();
            persistence.Dispose();
        }
    }

    [Test]
    public async Task HandleAsync_ShouldKeepMountedCreatureInSyncWithRiderPosition()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);

        try
        {
            var riderId = (Serial)0x00000020u;
            var mountId = (Serial)0x00000021u;
            await persistence.UnitOfWork.Mobiles.UpsertAsync(
                new UOMobileEntity
                {
                    Id = riderId,
                    Location = new(100, 100, 0),
                    MapId = 1,
                    MountedMobileId = mountId
                }
            );
            await persistence.UnitOfWork.Mobiles.UpsertAsync(
                new UOMobileEntity
                {
                    Id = mountId,
                    Location = new(100, 100, 0),
                    MapId = 1,
                    RiderMobileId = riderId
                }
            );

            var sessions = new FakeGameNetworkSessionService();
            var bus = new GameEventBusService();
            var service = new CharacterPositionPersistenceService(bus, sessions, persistence);
            var session = CreateSession(riderId, new(100, 100, 0), 1);
            session.Character!.MountedMobileId = mountId;
            sessions.Add(session);

            await service.HandleAsync(
                new(
                    session.SessionId,
                    session.CharacterId,
                    1,
                    2,
                    new(100, 100, 0),
                    new(500, 500, 10),
                    true
                )
            );

            var persistedRider = await persistence.UnitOfWork.Mobiles.GetByIdAsync(riderId);
            var persistedMount = await persistence.UnitOfWork.Mobiles.GetByIdAsync(mountId);

            Assert.Multiple(
                () =>
                {
                    Assert.That(persistedRider, Is.Not.Null);
                    Assert.That(persistedMount, Is.Not.Null);
                    Assert.That(persistedRider!.MapId, Is.EqualTo(2));
                    Assert.That(persistedRider.Location, Is.EqualTo(new Point3D(500, 500, 10)));
                    Assert.That(persistedMount!.MapId, Is.EqualTo(2));
                    Assert.That(persistedMount.Location, Is.EqualTo(new Point3D(500, 500, 10)));
                }
            );
        }
        finally
        {
            await persistence.StopAsync();
            persistence.Dispose();
        }
    }

    [Test]
    public async Task HandleAsync_ShouldThrottleSameSectorButPersistOnSectorChange()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);

        try
        {
            var sessions = new FakeGameNetworkSessionService();
            var bus = new GameEventBusService();
            var service = new CharacterPositionPersistenceService(bus, sessions, persistence);
            var session = CreateSession((Serial)0x00000003u, new(100, 100, 0), 1);
            sessions.Add(session);

            await service.HandleAsync(
                new(
                    session.SessionId,
                    session.CharacterId,
                    1,
                    1,
                    new(100, 100, 0),
                    new(101, 100, 0)
                )
            );
            await service.HandleAsync(
                new(
                    session.SessionId,
                    session.CharacterId,
                    1,
                    1,
                    new(101, 100, 0),
                    new(102, 100, 0)
                )
            );

            var persistedAfterThrottle = await persistence.UnitOfWork.Mobiles.GetByIdAsync(session.CharacterId);

            await service.HandleAsync(
                new(
                    session.SessionId,
                    session.CharacterId,
                    1,
                    1,
                    new(95, 100, 0),
                    new(96, 100, 0)
                )
            );

            var persistedAfterSectorChange = await persistence.UnitOfWork.Mobiles.GetByIdAsync(session.CharacterId);

            Assert.That(persistedAfterThrottle, Is.Not.Null);
            Assert.That(persistedAfterThrottle!.Location, Is.EqualTo(new Point3D(101, 100, 0)));
            Assert.That(persistedAfterSectorChange, Is.Not.Null);
            Assert.That(persistedAfterSectorChange!.Location, Is.EqualTo(new Point3D(96, 100, 0)));
        }
        finally
        {
            await persistence.StopAsync();
            persistence.Dispose();
        }
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

    private static GameSession CreateSession(Serial characterId, Point3D location, int mapId)
    {
        var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        return new(new(client))
        {
            CharacterId = characterId,
            Character = new()
            {
                Id = characterId,
                Location = location,
                MapId = mapId
            }
        };
    }
}
