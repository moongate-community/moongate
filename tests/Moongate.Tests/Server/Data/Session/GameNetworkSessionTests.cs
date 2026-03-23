using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Internal.Network;
using Moongate.Server.Data.Session;
using Moongate.Tests.Network.Support;
using Moongate.UO.Data.Version;

namespace Moongate.Tests.Server.Data.Session;

public sealed class GameNetworkSessionTests
{
    [Test]
    public void DisableEncryption_ShouldClearEncryptionAndRemoveMiddleware()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);
        session.EnableEncryption(new TestClientEncryption());

        session.DisableEncryption();

        Assert.Multiple(
            () =>
            {
                Assert.That(session.EncryptionEnabled, Is.False);
                Assert.That(session.Encryption, Is.Null);
                Assert.That(client.ContainsMiddleware<EncryptionMiddleware>(), Is.False);
            }
        );
    }

    [Test]
    public void EnableEncryption_ShouldStoreEncryptionAndAttachMiddleware()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);
        var encryption = new TestClientEncryption();

        session.EnableEncryption(encryption);

        Assert.Multiple(
            () =>
            {
                Assert.That(session.EncryptionEnabled, Is.True);
                Assert.That(session.Encryption, Is.SameAs(encryption));
                Assert.That(client.ContainsMiddleware<EncryptionMiddleware>(), Is.True);
            }
        );
    }

    [Test]
    public void SetClientType_WhenClientTypeIsClassic_ShouldNotMarkSessionAsEnhanced()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);

        session.SetClientType(ClientType.Classic);

        Assert.Multiple(
            () =>
            {
                Assert.That(session.ClientType, Is.EqualTo(ClientType.Classic));
                Assert.That(session.IsEnhancedClient, Is.False);
            }
        );
    }

    [Test]
    public void SetClientType_WhenClientTypeIsKr_ShouldMarkSessionAsEnhanced()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);

        session.SetClientType(ClientType.KR);

        Assert.Multiple(
            () =>
            {
                Assert.That(session.ClientType, Is.EqualTo(ClientType.KR));
                Assert.That(session.IsEnhancedClient, Is.True);
            }
        );
    }

    [Test]
    public void SetClientVersion_ShouldStoreClientVersion()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);
        var version = new ClientVersion("7.0.114.0");

        session.SetClientVersion(version);

        Assert.That(session.ClientVersion, Is.SameAs(version));
    }

    [Test]
    public void SetClientVersion_WhenVersionIsEnhanced_ShouldMarkSessionAsEnhanced()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameNetworkSession(client);

        session.SetClientVersion(new("67.0.114.0"));

        Assert.Multiple(
            () =>
            {
                Assert.That(session.ClientType, Is.EqualTo(ClientType.SA));
                Assert.That(session.IsEnhancedClient, Is.True);
            }
        );
    }
}
