using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class CharacterStatLockHandlerTests
{
    private sealed class InMemoryMobileService : IMobileService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();

        public void Add(UOMobileEntity mobile)
            => _mobiles[mobile.Id] = mobile;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _mobiles[mobile.Id] = mobile;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.FromResult(_mobiles.Remove(id));
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _mobiles.TryGetValue(id, out var mobile);

            return Task.FromResult(mobile);
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromException<UOMobileEntity>(new NotSupportedException());

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    [Test]
    public async Task HandleAsync_WhenRequestIsValid_ShouldPersistStrengthLock()
    {
        var mobileService = new InMemoryMobileService();
        var sessionService = new FakeGameNetworkSessionService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000001,
            Strength = 50,
            Dexterity = 50,
            Intelligence = 25
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };

        mobileService.Add(mobile);
        sessionService.Add(session);

        var handler = new CharacterStatLockHandler(outgoingQueue, mobileService, sessionService);

        await handler.HandleAsync(new(session.SessionId, Stat.Strength, UOSkillLock.Down));

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.StrengthLock, Is.EqualTo(UOSkillLock.Down));
                Assert.That(session.Character!.StrengthLock, Is.EqualTo(UOSkillLock.Down));
            }
        );
    }
}
