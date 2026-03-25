using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class TargetModuleTests
{
    private sealed class TargetModuleTestPlayerTargetService : IPlayerTargetService
    {
        public long LastSessionId { get; private set; }

        public TargetCursorSelectionType LastSelectionType { get; private set; }

        public TargetCursorType LastCursorType { get; private set; }

        public Action<PendingCursorCallback>? LastCallback { get; private set; }

        public Serial NextCursorId { get; set; } = (Serial)0x40001001u;

        public long LastCancelSessionId { get; private set; }

        public Serial LastCancelCursorId { get; private set; }

        public Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId)
        {
            LastCancelSessionId = sessionId;
            LastCancelCursorId = cursorId;

            return Task.CompletedTask;
        }

        public Task<Serial> SendTargetCursorAsync(
            long sessionId,
            Action<PendingCursorCallback> callback,
            TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
            TargetCursorType cursorType = TargetCursorType.Neutral
        )
        {
            LastSessionId = sessionId;
            LastSelectionType = selectionType;
            LastCursorType = cursorType;
            LastCallback = callback;

            return Task.FromResult(NextCursorId);
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public void Cancel_ShouldForwardToPlayerTargetService()
    {
        var targetService = new TargetModuleTestPlayerTargetService();
        var sessionService = new FakeGameNetworkSessionService();
        var module = new TargetModule(targetService, sessionService);

        var canceled = module.Cancel(11, (uint)0x4000100Au);

        Assert.Multiple(
            () =>
            {
                Assert.That(canceled, Is.True);
                Assert.That(targetService.LastCancelSessionId, Is.EqualTo(11));
                Assert.That(targetService.LastCancelCursorId, Is.EqualTo((Serial)0x4000100Au));
            }
        );
    }

    [Test]
    public void RequestLocation_ShouldForwardToPlayerTargetServiceAndReturnCursorId()
    {
        var targetService = new TargetModuleTestPlayerTargetService
        {
            NextCursorId = (Serial)0x4000100Bu
        };
        var sessionService = new FakeGameNetworkSessionService();
        var module = new TargetModule(targetService, sessionService);
        var callback = new Script().DoString("return function(_) end").Function;

        var cursorId = module.RequestLocation(11, callback, (int)TargetCursorType.Helpful);

        Assert.Multiple(
            () =>
            {
                Assert.That(cursorId, Is.EqualTo((uint)0x4000100Bu));
                Assert.That(targetService.LastSessionId, Is.EqualTo(11));
                Assert.That(targetService.LastSelectionType, Is.EqualTo(TargetCursorSelectionType.SelectLocation));
                Assert.That(targetService.LastCursorType, Is.EqualTo(TargetCursorType.Helpful));
                Assert.That(targetService.LastCallback, Is.Not.Null);
            }
        );
    }

    [Test]
    public void RequestLocation_CallbackShouldExecuteLuaClosureWithLocationPayload()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00001111u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00001111u,
                MapId = 4,
                Location = new(25, 35, 0)
            }
        };
        var targetService = new TargetModuleTestPlayerTargetService();
        var sessionService = new FakeGameNetworkSessionService();
        sessionService.Add(session);
        var module = new TargetModule(targetService, sessionService);
        var script = new Script();
        script.DoString("called = false; result_x = 0; result_y = 0; result_z = 0; result_map_id = 0");
        var callback = script.DoString(
            "return function(ctx) called = true; result_x = ctx.x; result_y = ctx.y; result_z = ctx.z; result_map_id = ctx.map_id end"
        ).Function;

        _ = module.RequestLocation(session.SessionId, callback);
        targetService.LastCallback!.Invoke(
            new(
                new TargetCursorCommandsPacket
                {
                    CursorTarget = TargetCursorSelectionType.SelectLocation,
                    CursorId = (Serial)0x4000100Cu,
                    CursorType = TargetCursorType.Neutral,
                    Location = new Point3D(111, 222, 7)
                }
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(script.Globals.Get("called").Boolean, Is.True);
                Assert.That(script.Globals.Get("result_x").Number, Is.EqualTo(111));
                Assert.That(script.Globals.Get("result_y").Number, Is.EqualTo(222));
                Assert.That(script.Globals.Get("result_z").Number, Is.EqualTo(7));
                Assert.That(script.Globals.Get("result_map_id").Number, Is.EqualTo(4));
            }
        );
    }
}
