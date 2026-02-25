using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Events;
using Moongate.Server.Data.Session;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Speech;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Speech;

public class SpeechServiceTests
{
    [Test]
    public async Task ProcessIncomingSpeechAsync_ShouldReturnEchoPacket_ForRegularSpeech()
    {
        var commandSystemService = new MockCommandSystemService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameNetworkSessionService = new SpeechServiceTestGameNetworkSessionService();
        var gameEventBusService = new GameEventBusService();
        var speechService = new SpeechService(
            commandSystemService,
            outgoingPacketQueue,
            gameNetworkSessionService,
            gameEventBusService
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000002,
                Name = "Tom",
                BaseBody = 0x0190
            }
        };

        var packet = new UnicodeSpeechPacket
        {
            MessageType = ChatMessageType.Regular,
            Hue = 0x0035,
            Font = 0x0003,
            Language = "ENU",
            Text = "hello"
        };

        var result = await speechService.ProcessIncomingSpeechAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.TypeOf<UnicodeSpeechMessagePacket>());
                Assert.That(commandSystemService.ExecuteCallCount, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task ProcessIncomingSpeechAsync_WhenSpeechStartsWithDot_ShouldDispatchCommandAndReturnNull()
    {
        var commandSystemService = new MockCommandSystemService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameNetworkSessionService = new SpeechServiceTestGameNetworkSessionService();
        var gameEventBusService = new GameEventBusService();
        var speechService = new SpeechService(
            commandSystemService,
            outgoingPacketQueue,
            gameNetworkSessionService,
            gameEventBusService
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000002,
                Name = "Tom",
                BaseBody = 0x0190
            }
        };

        var packet = new UnicodeSpeechPacket
        {
            MessageType = ChatMessageType.Regular,
            Hue = 0x0035,
            Font = 0x0003,
            Language = "ENU",
            Text = ".help"
        };

        var result = await speechService.ProcessIncomingSpeechAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Null);
                Assert.That(commandSystemService.ExecuteCallCount, Is.EqualTo(1));
                Assert.That(commandSystemService.LastCommandWithArgs, Is.EqualTo("help"));
                Assert.That(commandSystemService.LastSource, Is.EqualTo(CommandSourceType.InGame));
                Assert.That(commandSystemService.LastSession, Is.SameAs(session));
            }
        );
    }

    [Test]
    public async Task BroadcastFromServerAsync_ShouldEnqueueSystemMessage_ForAllActiveSessions()
    {
        var commandSystemService = new MockCommandSystemService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameNetworkSessionService = new SpeechServiceTestGameNetworkSessionService();
        var gameEventBusService = new GameEventBusService();
        var listener = new TestBroadcastFromServerEventListener();
        gameEventBusService.RegisterListener(listener);
        var speechService = new SpeechService(
            commandSystemService,
            outgoingPacketQueue,
            gameNetworkSessionService,
            gameEventBusService
        );
        using var clientA = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        using var clientB = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var sessionA = new GameSession(new(clientA))
        {
            Character = new()
            {
                Id = (Serial)0x00000002,
                Name = "Tom",
                BaseBody = 0x0190
            }
        };

        var sessionB = new GameSession(new(clientB))
        {
            Character = new()
            {
                Id = (Serial)0x00000003,
                Name = "Jerry",
                BaseBody = 0x0190
            }
        };

        gameNetworkSessionService.Add(sessionA);
        gameNetworkSessionService.Add(sessionB);

        var recipients = await speechService.BroadcastFromServerAsync("Welcome to Moongate");
        var dequeuedA = outgoingPacketQueue.TryDequeue(out var packetA);
        var dequeuedB = outgoingPacketQueue.TryDequeue(out var packetB);

        Assert.Multiple(
            () =>
            {
                Assert.That(recipients, Is.EqualTo(2));
                Assert.That(dequeuedA, Is.True);
                Assert.That(dequeuedB, Is.True);
                Assert.That(packetA.Packet, Is.TypeOf<UnicodeSpeechMessagePacket>());
                Assert.That(packetB.Packet, Is.TypeOf<UnicodeSpeechMessagePacket>());
                Assert.That(listener.EventCount, Is.EqualTo(1));
                Assert.That(listener.LastEvent.RecipientCount, Is.EqualTo(2));
                Assert.That(listener.LastEvent.Text, Is.EqualTo("Welcome to Moongate"));
                Assert.That(listener.LastEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }

    [Test]
    public async Task SendMessageFromServerAsync_ShouldEnqueueSystemMessage_ForTargetSession()
    {
        var commandSystemService = new MockCommandSystemService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameNetworkSessionService = new SpeechServiceTestGameNetworkSessionService();
        var gameEventBusService = new GameEventBusService();
        var listener = new TestSendMessageFromServerEventListener();
        gameEventBusService.RegisterListener(listener);
        var speechService = new SpeechService(
            commandSystemService,
            outgoingPacketQueue,
            gameNetworkSessionService,
            gameEventBusService
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000002,
                Name = "Tom",
                BaseBody = 0x0190
            }
        };

        var enqueued = await speechService.SendMessageFromServerAsync(session, "MOTD: Hello");
        var dequeued = outgoingPacketQueue.TryDequeue(out var outgoing);

        Assert.Multiple(
            () =>
            {
                Assert.That(enqueued, Is.True);
                Assert.That(dequeued, Is.True);
                Assert.That(outgoing.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(outgoing.Packet, Is.TypeOf<UnicodeSpeechMessagePacket>());
                Assert.That(listener.EventCount, Is.EqualTo(1));
                Assert.That(listener.LastEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(listener.LastEvent.Text, Is.EqualTo("MOTD: Hello"));
                Assert.That(listener.LastEvent.BaseEvent.Timestamp, Is.GreaterThan(0));
            }
        );
    }
}
