using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Types.Speech;

namespace Moongate.Tests.Server.Handlers;

public sealed class ChatHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_WhenOpenChatWindowPacketReceived_ShouldDelegateToChatSystemService()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var chatSystemService = new ChatHandlerTestChatSystemService();
        var handler = new ChatHandler(queue, chatSystemService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = new OpenChatWindowPacket();
        var payload = new byte[64];
        payload[0] = 0xB5;
        Assert.That(packet.TryParse(payload), Is.True);

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(chatSystemService.OpenCalls, Is.EqualTo(1));
                Assert.That(chatSystemService.LastSession, Is.SameAs(session));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenChatTextPacketReceived_ShouldDelegateToChatSystemService()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var chatSystemService = new ChatHandlerTestChatSystemService();
        var handler = new ChatHandler(queue, chatSystemService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = BuildChatPacket();

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(chatSystemService.ActionCalls, Is.EqualTo(1));
                Assert.That(chatSystemService.LastSession, Is.SameAs(session));
                Assert.That(chatSystemService.LastPacket, Is.SameAs(packet));
            }
        );
    }

    private static ChatTextPacket BuildChatPacket()
    {
        var writer = new Moongate.Network.Spans.SpanWriter(64, true);
        writer.Write((byte)0xB3);
        writer.Write((ushort)0);
        writer.WriteAscii("ENU", 4);
        writer.Write((short)ChatActionType.Close);
        writer.WriteBigUniNull(string.Empty);
        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        var packet = new ChatTextPacket();
        Assert.That(packet.TryParse(bytes), Is.True);

        return packet;
    }

    private sealed class ChatHandlerTestChatSystemService : IChatSystemService
    {
        public int OpenCalls { get; private set; }

        public int ActionCalls { get; private set; }

        public GameSession? LastSession { get; private set; }

        public ChatTextPacket? LastPacket { get; private set; }

        public Task OpenWindowAsync(GameSession session, CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            LastSession = session;

            return Task.CompletedTask;
        }

        public Task HandleChatActionAsync(GameSession session, ChatTextPacket packet, CancellationToken cancellationToken = default)
        {
            ActionCalls++;
            LastSession = session;
            LastPacket = packet;

            return Task.CompletedTask;
        }

        public Task RemoveSessionAsync(long sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }
}
