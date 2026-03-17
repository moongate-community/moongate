using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class MountMobileDoubleClickHandlerTests
{
    private sealed class TestMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> MobilesById { get; } = [];
        public Serial LastRiderId { get; private set; } = Serial.Zero;
        public Serial LastMountId { get; private set; } = Serial.Zero;
        public bool TryMountResult { get; set; } = true;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> DismountAsync(Serial riderId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(MobilesById.GetValueOrDefault(id));

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Moongate.UO.Data.Geometry.Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<bool> TryMountAsync(Serial riderId, Serial mountId, CancellationToken cancellationToken = default)
        {
            LastRiderId = riderId;
            LastMountId = mountId;

            return Task.FromResult(TryMountResult);
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Moongate.UO.Data.Geometry.Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    [Test]
    public async Task HandleAsync_WhenTargetIsMount_ShouldMountAndRefreshSession()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new FakeGameNetworkSessionService();
        var mobiles = new TestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var rider = new UOMobileEntity
        {
            Id = (Serial)0x200,
            Name = "rider",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var mount = new UOMobileEntity
        {
            Id = (Serial)0x300,
            Name = "horse",
            MapId = 1,
            Location = new(101, 100, 0)
        };
        mount.SetCustomString("is_mount", "true");
        mount.SetCustomString("mounted_display_item_id", "0x3EAA");
        mobiles.MobilesById[rider.Id] = rider;
        mobiles.MobilesById[mount.Id] = mount;
        var session = new GameSession(new(client))
        {
            CharacterId = rider.Id,
            Character = rider
        };
        sessions.Add(session);
        var handler = new MountMobileDoubleClickHandler(mobiles, sessions, outgoing);

        await handler.HandleAsync(new MobileDoubleClickEvent(session.SessionId, mount.Id));

        Assert.Multiple(
            () =>
            {
                Assert.That(mobiles.LastRiderId, Is.EqualTo(rider.Id));
                Assert.That(mobiles.LastMountId, Is.EqualTo(mount.Id));
                Assert.That(session.IsMounted, Is.True);
                Assert.That(session.Character!.MountedMobileId, Is.EqualTo(mount.Id));
                Assert.That(session.Character.MountedDisplayItemId, Is.EqualTo(0x3EAA));
                Assert.That(outgoing.TryDequeue(out var first), Is.True);
                Assert.That(first.Packet, Is.TypeOf<DrawPlayerPacket>());
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenTargetIsNotMount_ShouldDoNothing()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessions = new FakeGameNetworkSessionService();
        var mobiles = new TestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var rider = new UOMobileEntity
        {
            Id = (Serial)0x200,
            Name = "rider",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var target = new UOMobileEntity
        {
            Id = (Serial)0x300,
            Name = "not-a-mount",
            MapId = 1,
            Location = new(101, 100, 0)
        };
        mobiles.MobilesById[target.Id] = target;
        var session = new GameSession(new(client))
        {
            CharacterId = rider.Id,
            Character = rider
        };
        sessions.Add(session);
        var handler = new MountMobileDoubleClickHandler(mobiles, sessions, outgoing);

        await handler.HandleAsync(new MobileDoubleClickEvent(session.SessionId, target.Id));

        Assert.Multiple(
            () =>
            {
                Assert.That(mobiles.LastRiderId, Is.EqualTo(Serial.Zero));
                Assert.That(session.IsMounted, Is.False);
                Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(0));
            }
        );
    }
}
