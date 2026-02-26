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

namespace Moongate.Tests.Server.Services.Characters;

public sealed class CharacterPositionPersistenceServiceTests
{
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
                    new(100, 100, 0),
                    new(101, 100, 0)
                )
            );
            await service.HandleAsync(
                new(
                    session.SessionId,
                    session.CharacterId,
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
                    new(15, 100, 0),
                    new(16, 100, 0)
                )
            );

            var persistedAfterSectorChange = await persistence.UnitOfWork.Mobiles.GetByIdAsync(session.CharacterId);

            Assert.That(persistedAfterThrottle, Is.Not.Null);
            Assert.That(persistedAfterThrottle!.Location, Is.EqualTo(new Point3D(101, 100, 0)));
            Assert.That(persistedAfterSectorChange, Is.Not.Null);
            Assert.That(persistedAfterSectorChange!.Location, Is.EqualTo(new Point3D(16, 100, 0)));
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
