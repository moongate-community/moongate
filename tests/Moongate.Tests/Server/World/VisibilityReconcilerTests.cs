using System.Net.Sockets;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using SquidStd.Network.Client;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.World;

/// <summary>
/// The net under the reactive path. Its two properties are that it heals a session the reactive path
/// left wrong, and that it says nothing about one that is already right.
/// </summary>
public class VisibilityReconcilerTests
{
    [Fact]
    public void Tick_HealsASessionTheReactivePathLeftWrong()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));
        var ghost = world.Mobile(new(105, 100, 0));

        world.Visibility.Refresh(session);
        Assert.Contains(ghost.Id, world.Visibility.KnownTo(session));

        // Moved with no event published: exactly what a mutation path that forgot to tell the
        // visibility service looks like.
        world.Move(ghost, new(400, 400, 0));

        world.Reconciler.Tick();

        Assert.DoesNotContain(ghost.Id, world.Visibility.KnownTo(session));
    }

    // If a correct session produced packets, the net would be a storm every ten seconds.
    [Fact]
    public void Tick_OnACorrectSession_ChangesNothing()
    {
        var world = new Fixture();
        var session = world.Session(new(100, 100, 0));

        world.Mobile(new(105, 100, 0));
        world.Visibility.Refresh(session);

        var before = world.Visibility.KnownTo(session).ToHashSet();

        world.Reconciler.Tick();

        Assert.Equal(before, world.Visibility.KnownTo(session));
        Assert.True(world.Visibility.Refresh(session).IsEmpty);
    }

    // A session that has not picked a character yet has no view to reconcile.
    [Fact]
    public void Tick_SkipsSessionsWithoutACharacter()
    {
        var world = new Fixture();

        world.SessionWithoutCharacter();

        world.Reconciler.Tick();
    }

    private sealed class Fixture
    {
        private readonly FakePersistenceService _persistence = new();
        private readonly SpatialIndexService _spatial;
        private readonly StubSessionManager _sessions = new();

        public Fixture()
        {
            _spatial = new(_persistence, new StubLoopAffinity(), new EventBusService());
            Visibility = new VisibilityService(_spatial, new ItemService(_persistence), new VirtualSerialService());
            Reconciler = new(_sessions, Visibility, new StubGameLoopContext());
        }

        public VisibilityService Visibility { get; }

        public VisibilityReconciler Reconciler { get; }

        public PlayerSession Session(Point3D position)
        {
            var session = SessionWithoutCharacter();

            session.SetCharacter(Mobile(position));

            return session;
        }

        public PlayerSession SessionWithoutCharacter()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var session = new PlayerSession(new SquidStdTcpClient(socket, Stream.Null));

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

        public void Move(MobileEntity mobile, Point3D position)
        {
            mobile.Position = position;
            _persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            _spatial.AddOrUpdate(mobile);
        }
    }
}
