using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Modules;

public sealed class BankModuleTests
{
    private sealed class TestSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => [.. _sessions.Values];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.Values.FirstOrDefault(s => s.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class TestCharacterService : ICharacterService
    {
        public UOMobileEntity? Character { get; set; }
        public UOItemEntity? BankBox { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => throw new NotSupportedException();

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult(BankBox);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult(Character);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => throw new NotSupportedException();

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();
    }

    [Test]
    public void Open_ShouldEnqueueBankContainerForSession()
    {
        var sessionService = new TestSessionService();
        var characterService = new TestCharacterService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var character = new UOMobileEntity { Id = (Serial)0x00000044u, Name = "banker-test" };
        character.AddEquippedItem(ItemLayerType.Bank, (Serial)0x40000099u);
        characterService.Character = character;
        characterService.BankBox = new() { Id = (Serial)0x40000099u, ItemId = 0x09A8, GumpId = 0x0042 };

        var session = new GameSession(new(client))
        {
            CharacterId = character.Id,
            Character = character
        };
        sessionService.Add(session);

        var module = new BankModule(sessionService, characterService, queue);

        var ok = module.Open(session.SessionId);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());
            }
        );
    }
}
