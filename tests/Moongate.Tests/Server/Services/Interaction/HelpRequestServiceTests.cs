using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class HelpRequestServiceTests
{
    [Test]
    public async Task OpenAsync_WhenSessionHasCharacter_ShouldCallLuaHelpFunctionWithSessionAndCharacter()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00001234u
        };
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new HelpRequestService(scriptEngine);

        await service.OpenAsync(session);

        Assert.Multiple(
            () =>
            {
                Assert.That(scriptEngine.LastCallbackName, Is.EqualTo("on_help_request"));
                Assert.That(scriptEngine.LastCallbackArgs, Has.Length.EqualTo(2));
                Assert.That(scriptEngine.LastCallbackArgs![0], Is.EqualTo(session.SessionId));
                Assert.That(scriptEngine.LastCallbackArgs[1], Is.EqualTo((uint)session.CharacterId));
            }
        );
    }

    [Test]
    public async Task OpenAsync_WhenSessionHasNoCharacter_ShouldNotCallLuaHelpFunction()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var scriptEngine = new GameEventScriptBridgeTestScriptEngineService();
        var service = new HelpRequestService(scriptEngine);

        await service.OpenAsync(session);

        Assert.That(scriptEngine.LastCallbackName, Is.Null);
        Assert.That(scriptEngine.LastCallbackArgs, Is.Null);
    }
}
