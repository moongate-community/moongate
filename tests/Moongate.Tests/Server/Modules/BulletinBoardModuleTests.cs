using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Modules;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Modules;

public sealed class BulletinBoardModuleTests
{
    [Test]
    public void Open_ShouldForwardToService()
    {
        var service = new BulletinBoardModuleTestService();
        var module = new BulletinBoardModule(service);

        var ok = module.Open(42, 0x40000055u);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(service.LastSessionId, Is.EqualTo(42));
                Assert.That(service.LastBoardId, Is.EqualTo((Serial)0x40000055u));
            }
        );
    }

    private sealed class BulletinBoardModuleTestService : IBulletinBoardService
    {
        public long LastSessionId { get; private set; }
        public Serial LastBoardId { get; private set; }

        public Task<bool> OpenBoardAsync(long sessionId, Serial boardId)
        {
            LastSessionId = sessionId;
            LastBoardId = boardId;

            return Task.FromResult(true);
        }

        public Task<bool> SendSummaryAsync(GameSession session, uint boardId, uint messageId) => Task.FromResult(true);
        public Task<bool> SendMessageAsync(GameSession session, uint boardId, uint messageId) => Task.FromResult(true);
        public Task<bool> PostMessageAsync(GameSession session, Moongate.Network.Packets.Incoming.Interaction.BulletinBoardMessagesPacket packet) => Task.FromResult(true);
        public Task<bool> RemoveMessageAsync(GameSession session, uint boardId, uint messageId) => Task.FromResult(true);
    }
}
