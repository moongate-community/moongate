using System.Net.Sockets;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using SquidStd.Network.Client;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.World;

/// <summary>
/// Routing world events to the visibility service. The interesting one is a mobile that walks out of
/// someone's view: nothing about the destination says it happened.
/// </summary>
public class VisibilitySubscriberTests
{
    // Watching only the new position would leave the walker drawn on the client for ever.
    [Fact]
    public async Task MobileMoved_AwayFromAWatcher_UndrawsIt()
    {
        var world = new Fixture();
        var watcher = world.Session(new(100, 100, 0));
        var walker = world.Mobile(new(105, 100, 0));

        world.Visibility.Refresh(watcher);
        Assert.Contains(walker.Id, world.Visibility.KnownTo(watcher));

        var from = walker.Position;
        world.Move(walker, new(400, 400, 0));

        await world.Subscriber.OnMobileMoved(new(walker.Id, 1, from, 1, walker.Position), CancellationToken.None);

        Assert.DoesNotContain(walker.Id, world.Visibility.KnownTo(watcher));
    }

    [Fact]
    public async Task MobileMoved_TowardAWatcher_DrawsIt()
    {
        var world = new Fixture();
        var watcher = world.Session(new(100, 100, 0));
        var walker = world.Mobile(new(400, 400, 0));

        world.Visibility.Refresh(watcher);

        var from = walker.Position;
        world.Move(walker, new(105, 100, 0));

        await world.Subscriber.OnMobileMoved(new(walker.Id, 1, from, 1, walker.Position), CancellationToken.None);

        Assert.Contains(walker.Id, world.Visibility.KnownTo(watcher));
    }

    [Fact]
    public async Task SessionDestroyed_ForgetsWhatItKnew()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));

        world.Mobile(new(105, 100, 0));
        world.Visibility.Refresh(session);

        await world.Subscriber.OnSessionDestroyed(new(session), CancellationToken.None);

        Assert.Empty(world.Visibility.KnownTo(session));
    }

    [Fact]
    public async Task MobileDeleted_IsUndrawnForEveryoneWhoKnewIt()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var doomed = world.Mobile(new(105, 100, 0));

        world.Visibility.Refresh(session);

        await world.Subscriber.OnMobileDeleted(new(doomed), CancellationToken.None);

        Assert.DoesNotContain(doomed.Id, world.Visibility.KnownTo(session));
    }

    // An item dropped beside someone must reach them without waiting for the ten-second sweep.
    [Fact]
    public async Task ItemChanged_OnTheGroundInRange_IsDrawnAndRemembered()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));

        world.Visibility.Refresh(session);

        var item = world.Item(new(103, 100, 0));

        await world.Subscriber.OnItemChanged(new(item.Id), CancellationToken.None);

        Assert.Contains(item.Id, world.Visibility.KnownTo(session));
    }

    // Picked up is not "moved": the item leaves the world entirely, so range says nothing about it
    // and everyone who had it drawn has to be told directly.
    [Fact]
    public async Task ItemChanged_PickedUp_IsUndrawnForEveryoneWhoKnewIt()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var item = world.Item(new(103, 100, 0));

        world.Visibility.Refresh(session);
        Assert.Contains(item.Id, world.Visibility.KnownTo(session));

        world.PickUp(item);

        await world.Subscriber.OnItemChanged(new(item.Id), CancellationToken.None);

        Assert.DoesNotContain(item.Id, world.Visibility.KnownTo(session));
    }

    private sealed class Fixture
    {
        private readonly FakePersistenceService _persistence = new();
        private readonly SpatialIndexService _spatial;
        private readonly StubSessionManager _sessions = new();

        public Fixture()
        {
            _spatial = new(_persistence, new StubLoopAffinity(), new EventBusService());
            Items = new(_persistence);
            Visibility = new VisibilityService(_spatial, Items, new VirtualSerialService());
            Subscriber = new(Visibility, _sessions, _persistence, Items);
        }

        public ItemService Items { get; }

        public VisibilityService Visibility { get; }

        public VisibilitySubscriber Subscriber { get; }

        public PlayerSession Session(Point3D position)
        {
            var character = Mobile(position);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var session = new PlayerSession(new SquidStdTcpClient(socket, Stream.Null));

            session.SetCharacter(character);
            _sessions.Connections.Add(session);

            return session;
        }

        public MobileEntity Mobile(Point3D position)
        {
            var mobile = new MobileEntity { Name = "Someone", MapId = 1, Position = position };

            _persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            _spatial.AddOrUpdate(mobile);

            return mobile;
        }

        public ItemEntity Item(Point3D position)
        {
            var item = new ItemEntity { ItemId = 0x0EED, MapId = 1, Position = position };

            Items.Save(item);
            _spatial.AddOrUpdate(item);

            return item;
        }

        /// <summary>Off the ground and into a backpack: no longer in the world.</summary>
        public void PickUp(ItemEntity item)
        {
            item.ParentContainerId = (Serial)0xFFFF;
            Items.Save(item);
            _spatial.AddOrUpdate(item);
        }

        public void Move(MobileEntity mobile, Point3D position)
        {
            mobile.Position = position;
            _persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            _spatial.AddOrUpdate(mobile);
        }
    }
}
