using System.Net.Sockets;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Services.Gumps;
using Moongate.Server.Subscribers;
using SquidStd.Network.Client;

namespace Moongate.Tests.Server.Gumps;

/// <summary>
/// A gump outlives its own answer only until the player leaves. Without this the per-session map
/// grows for the life of the process, and a reused session id would inherit a stranger's open gumps
/// along with the button ids that pass validation.
/// </summary>
public class GumpSubscriberTests
{
    [Fact]
    public async Task SessionDestroyed_ForgetsThatSessionsGumps()
    {
        var gumps = new GumpService();
        var session = Session();

        gumps.Show(session, "bank", builder => builder.AddButton(0, 0, 1, 2, 7, 1, 0));
        Assert.Single(gumps.OpenFor(session));

        await new GumpSubscriber(gumps).OnSessionDestroyed(new(session), CancellationToken.None);

        Assert.Empty(gumps.OpenFor(session));
    }

    // A player who left with nothing open is not a special case, and must not throw.
    [Fact]
    public async Task SessionDestroyed_WithNothingOpen_IsHarmless()
    {
        var gumps = new GumpService();

        await new GumpSubscriber(gumps).OnSessionDestroyed(new(Session()), CancellationToken.None);
    }

    private static PlayerSession Session()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        return new(new SquidStdTcpClient(socket, Stream.Null));
    }
}
