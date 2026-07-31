using System.Net.Sockets;
using Moongate.Core.Extensions;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types.World;
using Moongate.Server.Scripting;
using Moongate.Server.Scripting.Refs;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;
using SquidStd.Network.Client;

namespace Moongate.Tests.Scripting;

/// <summary>
/// The module only translates: a mobile serial into a session, a selection name into an enum, and
/// a result into a table. The service is faked so those three are what is being tested.
/// </summary>
public class TargetModuleTests
{
    [Fact]
    public void Request_ForAnOnlineMobile_RaisesTheCursor()
    {
        var world = new Fixture();
        var session = world.Session();

        Assert.True(world.Module.Request(session.Character!.Id.Value, "location", world.Handler()));
        Assert.Equal(TargetSelectionType.Location, world.Targets.LastSelection);
    }

    // A serial nobody online owns raises nothing: there is no screen to put a cursor on.
    [Fact]
    public void Request_ForNobodyOnline_IsFalse()
    {
        var world = new Fixture();

        Assert.False(world.Module.Request(0xDEAD, "object", world.Handler()));
        Assert.Null(world.Targets.LastSelection);
    }

    // Refused rather than defaulting: silently targeting the wrong kind of thing is worse than
    // doing nothing.
    [Fact]
    public void Request_WithAnUnknownSelection_IsFalse()
    {
        var world = new Fixture();
        var session = world.Session();

        Assert.False(world.Module.Request(session.Character!.Id.Value, "sideways", world.Handler()));
        Assert.Null(world.Targets.LastSelection);
    }

    [Fact]
    public void TheCallbackReceivesTheResultTable()
    {
        var world = new Fixture();
        var session = world.Session();

        world.Module.Request(session.Character!.Id.Value, "location", world.Handler("seen = r.x"));
        world.Targets.Answer(new(TargetResultType.Location, default, new(42, 7, 0), 0));

        Assert.Equal(42d, world.Script.Globals.Get("seen").Number);
    }

    [Fact]
    public void Cancel_ForAnOnlineMobile_DelegatesToTheService()
    {
        var world = new Fixture();
        var session = world.Session();

        Assert.True(world.Module.Cancel(session.Character!.Id.Value));
    }

    private sealed class Fixture
    {
        private readonly FakePersistenceService _persistence = new();
        private readonly StubSessionManager _sessions = new();

        public Fixture()
        {
            Script = new();
            Targets = new();
            Module = new(Targets, _sessions, new TargetResultFactory(Script));
        }

        public Script Script { get; }

        public RecordingTargetService Targets { get; }

        public TargetModule Module { get; }

        /// <summary>A Lua closure running <paramref name="body" /> with the result bound to `r`.</summary>
        public Closure Handler(string body = "")
            => Script.DoString($"return function(r) {body} end").Function;

        public PlayerSession Session()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var session = new PlayerSession(new SquidStdTcpClient(socket, Stream.Null));
            var mobile = new MobileEntity { Name = "Squid", MapId = 1, Position = new(1, 1, 0) };

            _persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            session.SetCharacter(mobile);
            _sessions.Connections.Add(session);

            return session;
        }
    }

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
            => true;

        public TargetResultType Handle(PlayerSession session, TargetCursorResponsePacket packet)
            => TargetResultType.Cancelled;

        public void Forget(PlayerSession session) { }
    }
}
