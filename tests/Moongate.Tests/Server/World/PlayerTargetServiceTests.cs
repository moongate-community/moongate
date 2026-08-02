using System.Net.Sockets;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Types.World;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.World;

/// <summary>
/// One pending request per session, which is what the client can actually show. The test that
/// matters most is the superseding one: it is what stands in for moongatev2's whole expiry
/// dictionary and sweep timer.
/// </summary>
public class PlayerTargetServiceTests
{
    [Fact]
    public void Cancel_ReportsTheRequestCancelledAndClearsIt()
    {
        var service = new PlayerTargetService();
        var session = Session();
        TargetResult? seen = null;

        service.Request(session, TargetSelectionType.Object, r => seen = r);

        Assert.True(service.Cancel(session));
        Assert.True(seen!.Value.IsCancelled);
        Assert.False(service.Cancel(session));
    }

    // Logout drops it silently: there is nobody left to tell.
    [Fact]
    public void Forget_DropsThePendingWithoutInvokingIt()
    {
        var service = new PlayerTargetService();
        var session = Session();
        var called = false;

        var cursorId = service.Request(session, TargetSelectionType.Object, _ => called = true);

        service.Forget(session);

        Assert.False(called);
        Assert.Equal(TargetResultType.Cancelled, service.Handle(session, Clicked(cursorId, 0x4000_0001u, 1, 1, 0)));
    }

    [Fact]
    public void Handle_ALocationRequest_ReportsLocationEvenWithNoEntityClicked()
    {
        var service = new PlayerTargetService();
        var session = Session();

        var cursorId = service.Request(session, TargetSelectionType.Location, _ => { });

        Assert.Equal(TargetResultType.Location, service.Handle(session, Clicked(cursorId, 0u, 40, 50, 0)));
    }

    // What the client sends on Escape. Reporting it as an object at serial zero would be worse
    // than useless: callers would act on nothing.
    [Fact]
    public void Handle_ANothingPickedAnswer_IsCancelledRatherThanAnObject()
    {
        var service = new PlayerTargetService();
        var session = Session();
        TargetResult? seen = null;

        var cursorId = service.Request(session, TargetSelectionType.Object, r => seen = r);

        Assert.Equal(TargetResultType.Cancelled, service.Handle(session, Cancelled(cursorId)));
        Assert.True(seen!.Value.IsCancelled);
    }

    // A stale answer is what an ordinary client produces when a request was superseded, so it is
    // dropped rather than treated as tampering. No disconnect, unlike a gump's undrawn button.
    [Fact]
    public void Handle_WithAnIdThatIsNotPending_IsDropped()
    {
        var service = new PlayerTargetService();
        var session = Session();
        var called = false;

        service.Request(session, TargetSelectionType.Object, _ => called = true);

        Assert.Equal(TargetResultType.Cancelled, service.Handle(session, Clicked(999u, 0x4000_0001u, 1, 1, 0)));
        Assert.False(called);
    }

    [Fact]
    public void Handle_WithTheMatchingId_InvokesTheCallbackAndClearsIt()
    {
        var service = new PlayerTargetService();
        var session = Session();
        TargetResult? seen = null;

        var cursorId = service.Request(session, TargetSelectionType.Object, r => seen = r);

        Assert.Equal(TargetResultType.Object, service.Handle(session, Clicked(cursorId, 0x4000_0001u, 10, 20, 5)));
        Assert.Equal((Serial)0x4000_0001u, seen!.Value.Serial);
        Assert.Equal(10, seen.Value.Location.X);

        // Answered once: a second answer to the same cursor finds nothing pending.
        Assert.Equal(TargetResultType.Cancelled, service.Handle(session, Clicked(cursorId, 0x4000_0001u, 10, 20, 5)));
    }

    [Fact]
    public void Request_ReturnsACursorIdAndRecordsThePending()
    {
        var service = new PlayerTargetService();
        var session = Session();

        var cursorId = service.Request(session, TargetSelectionType.Object, _ => { });

        Assert.NotEqual(0u, cursorId);
    }

    // This replaces v2's expiry dictionary and its sweep timer: there is only ever one.
    [Fact]
    public void Request_WhileOneIsPending_CancelsTheFirst()
    {
        var service = new PlayerTargetService();
        var session = Session();
        TargetResult? first = null;

        var firstId = service.Request(session, TargetSelectionType.Object, r => first = r);
        service.Request(session, TargetSelectionType.Location, _ => { });

        Assert.True(first!.Value.IsCancelled);

        // And the superseded cursor's answer no longer matches anything.
        Assert.Equal(TargetResultType.Cancelled, service.Handle(session, Clicked(firstId, 0x4000_0001u, 1, 1, 0)));
    }

    private static TargetCursorResponsePacket Cancelled(uint cursorId)
        => new(cursorId, TargetSelectionType.Object, Serial.Zero, new(0xFFFF, 0xFFFF, 0), 0);

    private static TargetCursorResponsePacket Clicked(uint cursorId, uint serial, int x, int y, int z)
        => new(cursorId, TargetSelectionType.Object, new(serial), new(x, y, z), 0);

    private static PlayerSession Session()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        return new(new(socket, Stream.Null));
    }
}
