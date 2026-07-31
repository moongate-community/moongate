using System.Net.Sockets;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Abstractions.Types.World;
using Moongate.Server.Commands;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;
using SquidStd.Network.Client;

namespace Moongate.Tests.Server.Commands;

/// <summary>
/// The first consumer of the target cursor. What it says goes to whoever ran it — a reply, not a
/// broadcast. The service is faked here because the command's job is raising a cursor and putting
/// the answer into words, not correlating cursor ids.
/// </summary>
public class WhereCommandTests
{
    [Fact]
    public void Execute_RaisesAnObjectCursorForTheCaller()
    {
        var world = new Fixture();
        var session = world.Session();

        world.Command.Execute(Context(session.Character));

        Assert.Equal(TargetSelectionType.Object, world.Targets.LastSelection);
    }

    [Fact]
    public void Execute_AnEntityWasClicked_NamesIt()
    {
        var world = new Fixture();
        var replies = new List<string>();
        var clicked = world.Mobile("Lord British");

        world.Command.Execute(Context(world.Session().Character, replies.Add));
        world.Targets.Answer(new(TargetResultType.Object, clicked.Id, new(100, 200, 5), 0));

        Assert.Contains(replies, r => r.Contains("Lord British") && r.Contains("100"));
    }

    [Fact]
    public void Execute_GroundWasClicked_ReportsTheCoordinates()
    {
        var world = new Fixture();
        var replies = new List<string>();

        world.Command.Execute(Context(world.Session().Character, replies.Add));
        world.Targets.Answer(new(TargetResultType.Location, Serial.Zero, new(40, 50, 0), 0));

        Assert.Contains(replies, r => r.Contains("40") && r.Contains("50"));
    }

    // Escape is the common answer, and saying nothing would leave the player wondering whether the
    // command was broken.
    [Fact]
    public void Execute_ThePlayerCancelled_SaysSo()
    {
        var world = new Fixture();
        var replies = new List<string>();

        world.Command.Execute(Context(world.Session().Character, replies.Add));
        world.Targets.Answer(TargetResult.Cancelled);

        Assert.NotEmpty(replies);
    }

    // A console has no cursor to raise, so the command must say that rather than fail oddly.
    [Fact]
    public void Execute_WithNoActor_ExplainsAndRaisesNothing()
    {
        var world = new Fixture();
        var replies = new List<string>();

        world.Command.Execute(new(CommandSourceType.Console, null, [], replies.Add));

        Assert.NotEmpty(replies);
        Assert.Null(world.Targets.LastSelection);
    }

    private static CommandContext Context(MobileEntity? actor, Action<string>? reply = null)
        => new(CommandSourceType.InGame, actor, [], reply ?? (_ => { }));

    private sealed class Fixture
    {
        private readonly FakePersistenceService _persistence = new();
        private readonly StubSessionManager _sessions = new();

        public Fixture()
        {
            Targets = new();
            Command = new(Targets, _sessions, _persistence);
        }

        public RecordingTargetService Targets { get; }

        public WhereCommand Command { get; }

        public PlayerSession Session()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var session = new PlayerSession(new SquidStdTcpClient(socket, Stream.Null));

            session.SetCharacter(Mobile("Squid"));
            _sessions.Connections.Add(session);

            return session;
        }

        public MobileEntity Mobile(string name)
        {
            var mobile = new MobileEntity { Name = name, MapId = 1, Position = new(1, 1, 0) };

            _persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

            return mobile;
        }
    }

    /// <summary>Captures the request so a test can answer it, without correlating cursor ids.</summary>
    private sealed class RecordingTargetService : IPlayerTargetService
    {
        private Action<TargetResult>? _onTarget;

        public TargetSelectionType? LastSelection { get; private set; }

        public void Answer(TargetResult result)
            => _onTarget?.Invoke(result);

        public uint Request(PlayerSession session, TargetSelectionType selection, Action<TargetResult> onTarget)
        {
            LastSelection = selection;
            _onTarget = onTarget;

            return 1;
        }

        public bool Cancel(PlayerSession session)
            => false;

        public TargetResultType Handle(PlayerSession session, TargetCursorResponsePacket packet)
            => TargetResultType.Cancelled;

        public void Forget(PlayerSession session) { }
    }
}
