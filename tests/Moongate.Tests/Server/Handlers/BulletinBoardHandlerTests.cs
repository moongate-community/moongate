using Moongate.Network.Packets.Data.BulletinBoard;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Handlers;

public sealed class BulletinBoardHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_WhenRequestMessage_ShouldDelegateToService()
    {
        var service = new BulletinBoardHandlerTestService();
        var handler = new BulletinBoardHandler(new BasePacketListenerTestOutgoingPacketQueue(), service);
        var packet = new BulletinBoardMessagesPacketBuilder().Build(BulletinBoardSubcommand.RequestMessage, 0x40000055u, 0x40000099u);

        var result = await handler.HandlePacketAsync(new GameSession(default), packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.LastAction, Is.EqualTo("message"));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenPostMessage_ShouldDelegateToService()
    {
        var service = new BulletinBoardHandlerTestService();
        var handler = new BulletinBoardHandler(new BasePacketListenerTestOutgoingPacketQueue(), service);
        var packet = new BulletinBoardMessagesPacketBuilder().BuildPost(0x40000055u, 0u, "Subject", ["Body"]);

        var result = await handler.HandlePacketAsync(new GameSession(default), packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.LastAction, Is.EqualTo("post"));
            }
        );
    }

    private sealed class BulletinBoardHandlerTestService : IBulletinBoardService
    {
        public string? LastAction { get; private set; }

        public Task<bool> OpenBoardAsync(long sessionId, Serial boardId) => Task.FromResult(true);
        public Task<bool> SendSummaryAsync(GameSession session, uint boardId, uint messageId) { LastAction = "summary"; return Task.FromResult(true); }
        public Task<bool> SendMessageAsync(GameSession session, uint boardId, uint messageId) { LastAction = "message"; return Task.FromResult(true); }
        public Task<bool> PostMessageAsync(GameSession session, BulletinBoardMessagesPacket packet) { LastAction = "post"; return Task.FromResult(true); }
        public Task<bool> RemoveMessageAsync(GameSession session, uint boardId, uint messageId) { LastAction = "remove"; return Task.FromResult(true); }
    }

    private sealed class BulletinBoardMessagesPacketBuilder
    {
        public BulletinBoardMessagesPacket Build(BulletinBoardSubcommand subcommand, uint boardId, uint messageId)
        {
            var packet = new BulletinBoardMessagesPacket();
            var writer = new Moongate.Network.Spans.SpanWriter(64, true);
            writer.Write((byte)0x71);
            writer.Write((ushort)0);
            writer.Write((byte)subcommand);
            writer.Write(boardId);
            writer.Write(messageId);
            writer.WritePacketLength();
            var bytes = writer.ToArray();
            writer.Dispose();
            _ = packet.TryParse(bytes);

            return packet;
        }

        public BulletinBoardMessagesPacket BuildPost(uint boardId, uint parentId, string subject, IReadOnlyList<string> bodyLines)
        {
            var packet = new BulletinBoardMessagesPacket();
            var writer = new Moongate.Network.Spans.SpanWriter(128, true);
            writer.Write((byte)0x71);
            writer.Write((ushort)0);
            writer.Write((byte)BulletinBoardSubcommand.PostMessage);
            writer.Write(boardId);
            writer.Write(parentId);
            WriteAscii(ref writer, subject);
            writer.Write((byte)bodyLines.Count);
            foreach (var line in bodyLines)
            {
                WriteAscii(ref writer, line);
            }
            writer.WritePacketLength();
            var bytes = writer.ToArray();
            writer.Dispose();
            _ = packet.TryParse(bytes);

            return packet;
        }

        private static void WriteAscii(ref Moongate.Network.Spans.SpanWriter writer, string value)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(value);
            writer.Write((byte)(bytes.Length + 1));
            writer.Write(bytes);
            writer.Write((byte)0);
        }
    }
}
