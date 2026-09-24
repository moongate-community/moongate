using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Sessions;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Integration.Sessions;

public sealed class NetworkSessionTests
{
    [Fact]
    public void AbstractConnection_DetachPreservesMetadataWithoutClosingTransport()
    {
        using var connection = new ControlledNetworkConnection(7);
        var session = new NetworkSession(connection);
        Assert.Same(connection, session.Client);
        session.SetClientVersion("7.0.90.15");
        session.DetachClient();
        Assert.Null(session.Client);
        Assert.Equal("127.0.0.1:5151", session.RemoteEndPoint);
        Assert.Equal("127.0.0.1:2593", session.LocalEndPoint);
        Assert.Equal(NetworkSessionState.Disconnected, session.State);
        Assert.True(connection.IsConnected);
        Assert.Equal(0, connection.CloseCalls);
        Assert.Throws<InvalidOperationException>(() => session.SetSeed(1));
    }

    [Fact]
    public async Task DetachClient_RetainsConnectionSnapshotWithoutClosingSocket()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);
        var remoteEndPoint = fixture.Client.RemoteEndPoint?.ToString();
        var localEndPoint = fixture.Client.LocalEndPoint?.ToString();

        session.DetachClient();

        Assert.Equal(fixture.Client.SessionId, session.SessionId);
        Assert.Null(session.Client);
        Assert.Equal(NetworkSessionState.Disconnected, session.State);
        Assert.Equal(remoteEndPoint, session.RemoteEndPoint);
        Assert.Equal(localEndPoint, session.LocalEndPoint);
        Assert.Equal("127.0.0.1", session.RemoteIpAddress);
        Assert.Equal("127.0.0.1", session.LocalIpAddress);
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task DetachClient_WhenRepeated_RemainsDisconnected()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);

        session.DetachClient();
        session.DetachClient();

        Assert.Null(session.Client);
        Assert.Equal(NetworkSessionState.Disconnected, session.State);
        Assert.True(fixture.Client.IsConnected);
    }

    [Theory, InlineData(null), InlineData(""), InlineData(" \t ")]
    public async Task InvalidMutatorInputs_AfterDetachReportTerminalState(string? invalidVersion)
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);
        session.DetachClient();

        Assert.Throws<InvalidOperationException>(() => session.SetState((NetworkSessionState)int.MaxValue));
        Assert.Throws<InvalidOperationException>(() => session.SetClientVersion(invalidVersion!));

        Assert.Equal(NetworkSessionState.Disconnected, session.State);
        Assert.Null(session.ClientVersion);
    }

    [Fact]
    public async Task Mutators_AfterDetachAreRejectedWithoutChangingMetadata()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);
        session.SetSeed(7);
        session.SetClientVersion("7.0.90.15");
        session.DetachClient();

        Assert.Throws<InvalidOperationException>(() => session.SetState(NetworkSessionState.Login));
        Assert.Throws<InvalidOperationException>(() => session.SetSeed(8));
        Assert.Throws<InvalidOperationException>(() => session.SetClientVersion("7.0.91.0"));

        Assert.Equal(NetworkSessionState.Disconnected, session.State);
        Assert.Equal(7u, session.Seed);
        Assert.Equal("7.0.90.15", session.ClientVersion);
    }

    [Theory, InlineData(null), InlineData(""), InlineData(" \t ")]
    public async Task SetClientVersion_InvalidTextRejectedWithoutMutation(string? invalidVersion)
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);
        session.SetClientVersion(" 7.0.90.15 ");

        Assert.ThrowsAny<ArgumentException>(() => session.SetClientVersion(invalidVersion!));

        Assert.Equal(" 7.0.90.15 ", session.ClientVersion);
    }

    [Fact]
    public async Task SetSeed_ZeroIsStoredAsProtocolMetadata()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);

        session.SetSeed(0);

        Assert.Equal(0u, session.Seed);
    }

    [Fact]
    public async Task SetState_Disconnected_DetachesWithoutClosingSocket()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);

        session.SetState(NetworkSessionState.Disconnected);
        session.SetState(NetworkSessionState.Disconnected);

        Assert.Null(session.Client);
        Assert.Equal(NetworkSessionState.Disconnected, session.State);
        Assert.True(fixture.Client.IsConnected);
    }

    [Fact]
    public async Task SetState_UndefinedValueRejectedWithoutMutation()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new NetworkSession(fixture.Client);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetState((NetworkSessionState)int.MaxValue));

        Assert.Equal(NetworkSessionState.AwaitingSeed, session.State);
        Assert.Same(fixture.Client, session.Client);
    }
}
