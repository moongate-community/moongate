using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Services.Sessions;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services;

public sealed class GameNetworkSessionServiceTests
{
    private readonly List<MoongateTCPClient> _clientsToDispose = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var client in _clientsToDispose)
        {
            client.Dispose();
        }

        _clientsToDispose.Clear();
    }

    [Test]
    public void TryGetByCharacterId_ShouldReturnFalse_WhenMissing()
    {
        var service = new GameNetworkSessionService();
        _ = CreateSession(service, (Serial)0x00000011u);

        var found = service.TryGetByCharacterId((Serial)0x00000099u, out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void TryGetByCharacterId_ShouldReturnMatchingSession()
    {
        var service = new GameNetworkSessionService();
        var session = CreateSession(service, (Serial)0x00000042u);

        var found = service.TryGetByCharacterId((Serial)0x00000042u, out var resolved);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(resolved, Is.SameAs(session));
            }
        );
    }

    private GameSession CreateSession(GameNetworkSessionService service, Serial characterId)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var client = new MoongateTCPClient(socket);
        _clientsToDispose.Add(client);
        var session = service.GetOrCreate(client);
        session.CharacterId = characterId;

        return session;
    }
}
