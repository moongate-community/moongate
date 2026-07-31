using System.Net.Sockets;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Types.World;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using SquidStd.Network.Client;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.World;

/// <summary>
/// Each test here is a bug that would otherwise be found in play rather than in CI: a ghost left
/// behind, a mobile drawn twice, a client shown what it asked not to see, or a reconciliation sweep
/// that talks when it has nothing to say.
/// </summary>
public class VisibilityServiceTests
{
    [Fact]
    public void Refresh_OnAFreshSession_DrawsEverythingInRangeButNotTheSessionItself()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var other = world.Mobile(new(105, 100, 0));
        var item = world.Item(new(103, 100, 0));

        var delta = world.Visibility.Refresh(session);

        Assert.Equal([other.Id, item.Id], delta.Entered.Order().ToArray().Order());
        Assert.Empty(delta.Left);
        Assert.DoesNotContain(session.Character!.Id, world.Visibility.KnownTo(session));
    }

    // The ghost case. Without this the client keeps drawing something that walked away, until relog.
    [Fact]
    public void Refresh_AfterSomethingLeftRange_ForgetsIt()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var other = world.Mobile(new(105, 100, 0));

        world.Visibility.Refresh(session);
        world.Move(other, new(400, 400, 0));

        var delta = world.Visibility.Refresh(session);

        Assert.Equal([other.Id], delta.Left);
        Assert.Empty(world.Visibility.KnownTo(session));
    }

    // The reconciliation sweep runs every ten seconds over every session. If a correct session
    // produced packets, the safety net would be a packet storm.
    [Fact]
    public void Refresh_Twice_ChangesNothingTheSecondTime()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));

        world.Mobile(new(105, 100, 0));
        world.Visibility.Refresh(session);

        Assert.True(world.Visibility.Refresh(session).IsEmpty);
    }

    [Fact]
    public void Refresh_RespectsTheSessionsOwnViewRange()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));

        world.Mobile(new(115, 100, 0)); // 15 tiles: inside the default 18, outside a reduced 10
        session.SetViewRange(10);

        Assert.True(world.Visibility.Refresh(session).IsEmpty);
    }

    // The double-draw case: something already known that moves inside the view is a position
    // update, not a second incoming packet.
    [Fact]
    public void UpdateFor_AKnownMobileStillInRange_IsAMove()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var other = world.Mobile(new(105, 100, 0));

        world.Visibility.Refresh(session);
        world.Move(other, new(106, 100, 0));

        Assert.Equal(VisibilityChangeType.Moved, world.Visibility.UpdateFor(session, other));
    }

    [Fact]
    public void UpdateFor_AnUnknownMobileInRange_IsDrawnAndRemembered()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var other = world.Mobile(new(105, 100, 0));

        Assert.Equal(VisibilityChangeType.Drawn, world.Visibility.UpdateFor(session, other));
        Assert.Contains(other.Id, world.Visibility.KnownTo(session));
    }

    [Fact]
    public void UpdateFor_AKnownMobileNowOutOfRange_IsUndrawnAndForgotten()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var other = world.Mobile(new(105, 100, 0));

        world.Visibility.Refresh(session);
        world.Move(other, new(400, 400, 0));

        Assert.Equal(VisibilityChangeType.Undrawn, world.Visibility.UpdateFor(session, other));
        Assert.Empty(world.Visibility.KnownTo(session));
    }

    // Something the client never had and still cannot see is not news.
    [Fact]
    public void UpdateFor_AnUnknownMobileOutOfRange_IsNothing()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var other = world.Mobile(new(400, 400, 0));

        Assert.Equal(VisibilityChangeType.None, world.Visibility.UpdateFor(session, other));
    }

    [Fact]
    public void Forget_DropsTheWholeKnownSet()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));

        world.Mobile(new(105, 100, 0));
        world.Visibility.Refresh(session);

        world.Visibility.Forget(session);

        Assert.Empty(world.Visibility.KnownTo(session));
    }

    private sealed class Fixture
    {
        private readonly FakePersistenceService _persistence = new();
        private readonly SpatialIndexService _spatial;
        private readonly ItemService _items;

        public Fixture()
        {
            _spatial = new(_persistence, new StubLoopAffinity(), new EventBusService());
            _items = new(_persistence);
            Visibility = new VisibilityService(_spatial, _items, new VirtualSerialService());
        }

        public VisibilityService Visibility { get; }

        public PlayerSession Session(Point3D position)
        {
            var character = Mobile(position);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var session = new PlayerSession(new SquidStdTcpClient(socket, Stream.Null));

            session.SetCharacter(character);

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

            _items.Save(item);
            _spatial.AddOrUpdate(item);

            return item;
        }

        public void Move(MobileEntity mobile, Point3D position)
        {
            mobile.Position = position;
            _persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            _spatial.AddOrUpdate(mobile);
        }
    }
}
