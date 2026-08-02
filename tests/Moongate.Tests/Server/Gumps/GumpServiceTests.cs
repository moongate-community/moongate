using System.Net.Sockets;
using Moongate.Server.Abstractions.Data.Gumps;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Types.Gumps;
using Moongate.Server.Services.Gumps;
using SquidStd.Network.Client;

namespace Moongate.Tests.Server.Gumps;

/// <summary>
/// The service is the only thing that knows what it drew, so it is the only thing that can tell a
/// genuine answer from an invented one. Every test here is about that boundary.
/// </summary>
public class GumpServiceTests
{
    [Fact]
    public void CloseAll_ForgetsEverythingTheSessionHadOpen()
    {
        var service = new GumpService();
        var session = Session();

        service.Show(session, "a", _ => { });
        service.Show(session, "b", _ => { });

        Assert.Equal(2, service.CloseAll(session));
        Assert.Empty(service.OpenFor(session));
    }

    [Fact]
    public void Close_ForgetsOnlyTheNamedGump()
    {
        var service = new GumpService();
        var session = Session();

        service.Show(session, "a", _ => { });
        service.Show(session, "b", _ => { });

        Assert.True(service.Close(session, "a"));
        Assert.False(service.Close(session, "a"));
        Assert.Equal("b", Assert.Single(service.OpenFor(session)).GumpId);
    }

    [Fact]
    public void HandleResponse_ForAGumpThatWasNeverOpened_IsNotOpen()
    {
        var service = new GumpService();
        var session = Session();

        var rejection = service.HandleResponse(session, 999u, 1, 0, [], new Dictionary<int, string>());

        Assert.Equal(GumpRejectionType.NotOpen, rejection);
    }

    // The gump is forgotten before its callback runs, so a replayed answer finds nothing open. That
    // is the same path a fabricated serial takes, which is what makes replay uninteresting.
    [Fact]
    public void HandleResponse_Twice_IsNotOpenTheSecondTime()
    {
        var service = new GumpService();
        var session = Session();
        var calls = 0;

        service.Show(session, "test", builder => builder.AddButton(0, 0, 1, 2, 7, 1, 0), _ => calls++);

        var open = Assert.Single(service.OpenFor(session));

        Assert.Equal(
            GumpRejectionType.None,
            service.HandleResponse(session, open.Serial, open.TypeId, 7, [], new Dictionary<int, string>())
        );
        Assert.Equal(
            GumpRejectionType.NotOpen,
            service.HandleResponse(session, open.Serial, open.TypeId, 7, [], new Dictionary<int, string>())
        );
        Assert.Equal(1, calls);
    }

    [Fact]
    public void HandleResponse_Valid_RunsTheCallbackWithWhatThePlayerDid()
    {
        var service = new GumpService();
        var session = Session();
        GumpResponse? seen = null;

        service.Show(
            session,
            "test",
            builder =>
            {
                builder.AddButton(0, 0, 1, 2, 7, 1, 0);
                builder.AddCheck(0, 20, 1, 2, false, 3);
            },
            response => seen = response
        );

        var open = Assert.Single(service.OpenFor(session));
        var rejection = service.HandleResponse(session, open.Serial, open.TypeId, 7, [3], new Dictionary<int, string>());

        Assert.Equal(GumpRejectionType.None, rejection);
        Assert.Equal(7, seen!.Value.Button);
        Assert.Equal([3], seen.Value.Switches);
    }

    [Fact]
    public void HandleResponse_WithAnUndrawnButton_IsRejectedAndTheGumpStaysOpen()
    {
        var service = new GumpService();
        var session = Session();

        service.Show(session, "test", builder => builder.AddButton(0, 0, 1, 2, 7, 1, 0));

        var open = Assert.Single(service.OpenFor(session));
        var rejection = service.HandleResponse(session, open.Serial, open.TypeId, 99, [], new Dictionary<int, string>());

        Assert.Equal(GumpRejectionType.UnknownButton, rejection);
        Assert.Single(service.OpenFor(session));
    }

    // The type id decides whether the client replaces a gump already on screen, so it has to survive
    // a restart. A randomised hash would quietly break that between boots.
    [Fact]
    public void Show_TheSameGumpId_AlwaysGetsTheSameTypeId()
    {
        var service = new GumpService();
        var session = Session();

        service.Show(session, "bank", _ => { });
        service.Show(session, "bank", _ => { });

        var open = service.OpenFor(session);

        Assert.Equal(2, open.Count);
        Assert.Equal(open[0].TypeId, open[1].TypeId);
        Assert.NotEqual(open[0].Serial, open[1].Serial);
    }

    private static PlayerSession Session()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var client = new SquidStdTcpClient(socket, Stream.Null);

        return new(client);
    }
}
