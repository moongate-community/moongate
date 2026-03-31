using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class QuestGumpRequestHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_WhenQuestButtonPacketIsReceived_ShouldPublishQuestJournalRequestedEvent()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new QuestGumpRequestHandler(queue, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005001u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00005001u,
                IsPlayer = true
            }
        };
        var packet = new QuestGumpRequestPacket();
        Assert.That(
            packet.TryParse(
                new byte[]
                {
                    0xD7,
                    0x00,
                    0x0A,
                    0x00,
                    0x00,
                    0x50,
                    0x01,
                    0x00,
                    0x32,
                    0x07
                }
            ),
            Is.True
        );

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                var events = eventBus.Events.OfType<QuestJournalRequestedEvent>().ToList();
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].SessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenSessionCharacterIsMissing_ShouldNotPublishQuestJournalRequestedEvent()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new QuestGumpRequestHandler(queue, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005001u,
            Character = null
        };
        var packet = new QuestGumpRequestPacket();
        Assert.That(
            packet.TryParse(
                new byte[]
                {
                    0xD7,
                    0x00,
                    0x0A,
                    0xDE,
                    0xAD,
                    0xBE,
                    0xEF,
                    0x00,
                    0x32,
                    0x07
                }
            ),
            Is.True
        );

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<QuestJournalRequestedEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenPacketSerialDoesNotMatchSessionCharacter_ShouldNotPublishQuestJournalRequestedEvent()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new QuestGumpRequestHandler(queue, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005001u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00005001u,
                IsPlayer = true
            }
        };
        var packet = new QuestGumpRequestPacket();
        Assert.That(
            packet.TryParse(
                new byte[]
                {
                    0xD7,
                    0x00,
                    0x0A,
                    0xDE,
                    0xAD,
                    0xBE,
                    0xEF,
                    0x00,
                    0x32,
                    0x07
                }
            ),
            Is.True
        );

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<QuestJournalRequestedEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenQuestLeafPayloadIsEmpty_ShouldNotPublishQuestJournalRequestedEvent()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new QuestGumpRequestHandler(queue, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005001u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00005001u,
                IsPlayer = true
            }
        };
        var packet = new QuestGumpRequestPacket();
        Assert.That(
            packet.TryParse(
                new byte[]
                {
                    0xD7,
                    0x00,
                    0x09,
                    0x00,
                    0x00,
                    0x50,
                    0x01,
                    0x00,
                    0x32
                }
            ),
            Is.True
        );

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<QuestJournalRequestedEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenQuestLeafPayloadByteIsWrong_ShouldNotPublishQuestJournalRequestedEvent()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new QuestGumpRequestHandler(queue, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005001u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00005001u,
                IsPlayer = true
            }
        };
        var packet = new QuestGumpRequestPacket();
        Assert.That(
            packet.TryParse(
                new byte[]
                {
                    0xD7,
                    0x00,
                    0x0A,
                    0x00,
                    0x00,
                    0x50,
                    0x01,
                    0x00,
                    0x32,
                    0x08
                }
            ),
            Is.True
        );

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<QuestJournalRequestedEvent>(), Is.Empty);
            }
        );
    }
}
