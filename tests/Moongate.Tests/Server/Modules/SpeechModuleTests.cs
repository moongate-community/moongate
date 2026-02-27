using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Modules;

public class SpeechModuleTests
{
    private sealed class SpeechModuleTestSpeechService : ISpeechService
    {
        public int BroadcastResult { get; set; }

        public int BroadcastCalls { get; private set; }

        public string? LastBroadcastText { get; private set; }

        public int SendCalls { get; private set; }

        public string? LastSendText { get; private set; }

        public long? LastSendSessionId { get; private set; }

        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            BroadcastCalls++;
            LastBroadcastText = text;

            return Task.FromResult(BroadcastResult);
        }

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<UnicodeSpeechMessagePacket?>(null);

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            SendCalls++;
            LastSendText = text;
            LastSendSessionId = session.SessionId;

            return Task.FromResult(true);
        }
    }

    [Test]
    public void Broadcast_ShouldForwardToSpeechService_AndReturnRecipientCount()
    {
        var speechService = new SpeechModuleTestSpeechService { BroadcastResult = 7 };
        var sessionService = new FakeGameNetworkSessionService();
        var module = new SpeechModule(speechService, sessionService);

        var recipients = module.Broadcast("server message");

        Assert.Multiple(
            () =>
            {
                Assert.That(recipients, Is.EqualTo(7));
                Assert.That(speechService.BroadcastCalls, Is.EqualTo(1));
                Assert.That(speechService.LastBroadcastText, Is.EqualTo("server message"));
            }
        );
    }

    [Test]
    public void Say_WhenCharacterSessionExists_ShouldForwardToSpeechService()
    {
        var speechService = new SpeechModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var module = new SpeechModule(speechService, sessionService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client)) { CharacterId = (Serial)0x22 };
        sessionService.Add(session);

        var sent = module.Say((uint)session.CharacterId, "speaking");

        Assert.Multiple(
            () =>
            {
                Assert.That(sent, Is.True);
                Assert.That(speechService.SendCalls, Is.EqualTo(1));
                Assert.That(speechService.LastSendSessionId, Is.EqualTo(session.SessionId));
                Assert.That(speechService.LastSendText, Is.EqualTo("speaking"));
            }
        );
    }

    [Test]
    public void Send_WhenSessionExists_ShouldForwardToSpeechService()
    {
        var speechService = new SpeechModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var module = new SpeechModule(speechService, sessionService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client)) { CharacterId = (Serial)0x10 };
        sessionService.Add(session);

        var sent = module.Send(session.SessionId, "hello");

        Assert.Multiple(
            () =>
            {
                Assert.That(sent, Is.True);
                Assert.That(speechService.SendCalls, Is.EqualTo(1));
                Assert.That(speechService.LastSendSessionId, Is.EqualTo(session.SessionId));
                Assert.That(speechService.LastSendText, Is.EqualTo("hello"));
            }
        );
    }
}
