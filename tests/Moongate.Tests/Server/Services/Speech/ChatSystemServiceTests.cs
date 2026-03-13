using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Spans;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Speech;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types.Speech;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Speech;

public sealed class ChatSystemServiceTests
{
    private readonly List<MoongateTCPClient> _clientsToDispose = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var client in _clientsToDispose)
        {
            client.Dispose();
        }
    }

    [Test]
    public async Task OpenWindowAsync_ShouldCreateChatUserAndSendOpenWindowCommand()
    {
        var service = CreateService(out var queue, out var sessionService);
        var session = CreateSession("Alice");
        sessionService.Add(session);

        await service.OpenWindowAsync(session);

        Assert.That(queue.TryDequeue(out var outgoing), Is.True);
        Assert.That(outgoing.SessionId, Is.EqualTo(session.SessionId));
        Assert.That(outgoing.Packet, Is.TypeOf<ChatCommandPacket>());

        var packet = (ChatCommandPacket)outgoing.Packet;

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.Command, Is.EqualTo((ushort)ChatCommandType.OpenChatWindow));
                Assert.That(packet.Param1, Is.EqualTo("Alice"));
            }
        );
    }

    [Test]
    public async Task HandleChatActionAsync_ShouldCreateAndJoinChannels()
    {
        var service = CreateService(out var queue, out var sessionService);
        var alice = CreateSession("Alice");
        var bob = CreateSession("Bob");
        sessionService.Add(alice);
        sessionService.Add(bob);

        await service.OpenWindowAsync(alice);
        await service.OpenWindowAsync(bob);
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.CreateConference, "General"));

        var alicePackets = Drain(queue).Where(x => x.SessionId == alice.SessionId).Select(x => (ChatCommandPacket)x.Packet).ToList();

        Assert.That(alicePackets.Any(p => p.Command == (ushort)ChatCommandType.JoinedChannel && p.Param1 == "General"), Is.True);
        Assert.That(alicePackets.Any(p => p.Command == (ushort)ChatCommandType.AddUserToChannel && p.Param1.EndsWith("Alice", StringComparison.Ordinal)), Is.True);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.JoinConference, "\"General\""));

        var packets = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();

        Assert.That(packets.Any(p => p.SessionId == bob.SessionId && p.Packet.Command == (ushort)ChatCommandType.JoinedChannel && p.Packet.Param1 == "General"), Is.True);
        Assert.That(packets.Any(p => p.Packet.Command == (ushort)ChatCommandType.AddUserToChannel && p.Packet.Param1.EndsWith("Bob", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task HandleChatActionAsync_ShouldRouteChannelMessagesToMembers()
    {
        var service = CreateService(out var queue, out var sessionService);
        var alice = CreateSession("Alice");
        var bob = CreateSession("Bob");
        sessionService.Add(alice);
        sessionService.Add(bob);

        await service.OpenWindowAsync(alice);
        await service.OpenWindowAsync(bob);
        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.CreateConference, "General"));
        Drain(queue);
        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.JoinConference, "General"));
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.Message, "hello conference"));

        var packets = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();

        Assert.That(packets.Any(p => p.SessionId == bob.SessionId && p.Packet.Command == 57 && p.Packet.Param2 == "hello conference"), Is.True);
    }

    [Test]
    public async Task HandleChatActionAsync_ShouldRespectIgnoreAndPrivateMessageToggles()
    {
        var service = CreateService(out var queue, out var sessionService);
        var alice = CreateSession("Alice");
        var bob = CreateSession("Bob");
        sessionService.Add(alice);
        sessionService.Add(bob);

        await service.OpenWindowAsync(alice);
        await service.OpenWindowAsync(bob);
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.SendPrivateMessage, "Bob hello"));

        var firstAttempt = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(firstAttempt.Any(p => p.SessionId == bob.SessionId && p.Packet.Command == 59 && p.Packet.Param2 == "hello"), Is.True);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.Ignore, "Alice"));
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.SendPrivateMessage, "Bob blocked"));

        var secondAttempt = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(secondAttempt.Any(p => p.SessionId == alice.SessionId && p.Packet.Command == 35), Is.True);
        Assert.That(secondAttempt.Any(p => p.SessionId == bob.SessionId && p.Packet.Command == 59 && p.Packet.Param2 == "blocked"), Is.False);
    }

    [Test]
    public async Task HandleChatActionAsync_ShouldApplyVoiceRulesAndKickUsers()
    {
        var service = CreateService(out var queue, out var sessionService);
        var alice = CreateSession("Alice");
        var bob = CreateSession("Bob");
        sessionService.Add(alice);
        sessionService.Add(bob);

        await service.OpenWindowAsync(alice);
        await service.OpenWindowAsync(bob);
        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.CreateConference, "General"));
        Drain(queue);
        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.JoinConference, "General"));
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.RestrictDefaultSpeakingPrivileges, string.Empty));
        Drain(queue);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.Message, "blocked"));

        var blockedPackets = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(blockedPackets.Any(p => p.SessionId == bob.SessionId && p.Packet.Command == 36), Is.True);
        Assert.That(blockedPackets.Any(p => p.SessionId == alice.SessionId && p.Packet.Command == 57 && p.Packet.Param2 == "blocked"), Is.False);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.GrantSpeakingPrivileges, "Bob"));
        Drain(queue);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.Message, "allowed"));

        var allowedPackets = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(allowedPackets.Any(p => p.SessionId == alice.SessionId && p.Packet.Command == 57 && p.Packet.Param2 == "allowed"), Is.True);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.Kick, "Bob"));
        Drain(queue);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.Message, "afterkick"));

        var afterKickPackets = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(afterKickPackets.Any(p => p.SessionId == bob.SessionId && p.Packet.Command == 31), Is.True);
    }

    [Test]
    public async Task HandleChatActionAsync_ShouldRenameProtectAndWhoIsChannels()
    {
        var service = CreateService(out var queue, out var sessionService);
        var alice = CreateSession("Alice");
        var bob = CreateSession("Bob");
        var charlie = CreateSession("Charlie");
        sessionService.Add(alice);
        sessionService.Add(bob);
        sessionService.Add(charlie);

        await service.OpenWindowAsync(alice);
        await service.OpenWindowAsync(bob);
        await service.OpenWindowAsync(charlie);
        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.CreateConference, "General"));
        Drain(queue);
        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.JoinConference, "General"));
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.RenameConference, "Renamed"));
        var renamePackets = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(renamePackets.Any(p => p.Packet.Command == (ushort)ChatCommandType.JoinedChannel && p.Packet.Param1 == "Renamed"), Is.True);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.ChangePassword, "secret"));
        Drain(queue);

        await service.HandleChatActionAsync(charlie, BuildChatPacket(ChatActionType.JoinConference, "\"Renamed\" wrong"));
        var wrongPassword = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(wrongPassword.Any(p => p.SessionId == charlie.SessionId && p.Packet.Command == 34), Is.True);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.HideCharacterName, string.Empty));
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.Whois, "Bob"));
        var hiddenWhoIs = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(hiddenWhoIs.Any(p => p.SessionId == alice.SessionId && p.Packet.Command == 41), Is.True);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.ShowCharacterName, string.Empty));
        Drain(queue);

        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.Whois, "Bob"));
        var visibleWhoIs = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(visibleWhoIs.Any(p => p.SessionId == alice.SessionId && p.Packet.Command == 43 && p.Packet.Param2 == "Bob"), Is.True);

        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.Emote, "waves"));
        var emotePackets = Drain(queue).Select(x => (x.SessionId, Packet: (ChatCommandPacket)x.Packet)).ToList();
        Assert.That(emotePackets.Any(p => p.SessionId == alice.SessionId && p.Packet.Command == 58 && p.Packet.Param2 == "waves"), Is.True);
    }

    [Test]
    public async Task RemoveSessionAsync_ShouldCleanupMembershipAndRemoveEmptyChannels()
    {
        var service = CreateService(out var queue, out var sessionService);
        var alice = CreateSession("Alice");
        var bob = CreateSession("Bob");
        sessionService.Add(alice);
        sessionService.Add(bob);

        await service.OpenWindowAsync(alice);
        await service.OpenWindowAsync(bob);
        await service.HandleChatActionAsync(alice, BuildChatPacket(ChatActionType.CreateConference, "General"));
        Drain(queue);
        await service.HandleChatActionAsync(bob, BuildChatPacket(ChatActionType.JoinConference, "General"));
        Drain(queue);

        await service.RemoveSessionAsync(bob.SessionId);
        Drain(queue);

        await service.RemoveSessionAsync(alice.SessionId);
        Drain(queue);

        var charlie = CreateSession("Charlie");
        sessionService.Add(charlie);
        await service.OpenWindowAsync(charlie);
        Drain(queue);

        await service.HandleChatActionAsync(charlie, BuildChatPacket(ChatActionType.JoinConference, "General"));
    }

    private ChatSystemService CreateService(out BasePacketListenerTestOutgoingPacketQueue queue, out ChatSystemTestGameNetworkSessionService sessionService)
    {
        queue = new BasePacketListenerTestOutgoingPacketQueue();
        sessionService = new ChatSystemTestGameNetworkSessionService();

        return new ChatSystemService(queue, sessionService);
    }

    private GameSession CreateSession(string characterName)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var client = new MoongateTCPClient(socket);
        _clientsToDispose.Add(client);

        var session = new GameSession(new GameNetworkSession(client))
        {
            CharacterId = (Serial)client.SessionId,
            Character = new UOMobileEntity
            {
                Id = (Serial)client.SessionId,
                Name = characterName
            }
        };

        return session;
    }

    private static ChatTextPacket BuildChatPacket(ChatActionType actionId, string payload)
    {
        var writer = new SpanWriter(256, true);
        writer.Write((byte)0xB3);
        writer.Write((ushort)0);
        writer.WriteAscii("ENU", 4);
        writer.Write((short)actionId);
        writer.WriteBigUniNull(payload);
        writer.WritePacketLength();

        var bytes = writer.ToArray();
        writer.Dispose();

        var packet = new ChatTextPacket();
        Assert.That(packet.TryParse(bytes), Is.True);

        return packet;
    }

    private static List<Moongate.Server.Data.Packets.OutgoingGamePacket> Drain(BasePacketListenerTestOutgoingPacketQueue queue)
    {
        var packets = new List<Moongate.Server.Data.Packets.OutgoingGamePacket>();

        while (queue.TryDequeue(out var outgoing))
        {
            packets.Add(outgoing);
        }

        return packets;
    }

    private sealed class ChatSystemTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.Values.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.Values.FirstOrDefault(x => x.CharacterId == characterId)!;

            return session is not null;
        }
    }
}
