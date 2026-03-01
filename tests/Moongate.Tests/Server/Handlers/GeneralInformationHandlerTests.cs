using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Party;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public class GeneralInformationHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_ShouldPublishPartySystemCommandEvent_ForSubcommand06()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.PartySystem,
            new byte[] { 0x04, 0xAA, 0xBB }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<PartySystemCommandEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.Subcommand, Is.EqualTo(0x04));
                Assert.That(gameEvent.Payload, Is.EqualTo(new byte[] { 0xAA, 0xBB }));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishStatLockChangeRequestedEvent_ForSubcommand1A()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.StatLockChange,
            new byte[] { 0x01, 0x02 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<StatLockChangeRequestedEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.Stat, Is.EqualTo(Stat.Dexterity));
                Assert.That(gameEvent.LockState, Is.EqualTo(UOSkillLock.Locked));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishTargetedItemUseEvent_ForSubcommand2C()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var itemSerial = (Serial)0x40000010u;
        var targetSerial = (Serial)0x00000002u;
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.UseTargetedItem,
            new byte[] { 0x40, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x02 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<TargetedItemUseEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.ItemSerial, Is.EqualTo(itemSerial));
                Assert.That(gameEvent.TargetSerial, Is.EqualTo(targetSerial));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishTargetedSpellCastEvent_ForSubcommand2D()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.CastTargetedSpell,
            new byte[] { 0x00, 0x2D, 0x00, 0x00, 0x00, 0x05 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<TargetedSpellCastEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.SpellId, Is.EqualTo((ushort)0x002D));
                Assert.That(gameEvent.TargetSerial, Is.EqualTo((Serial)0x00000005u));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishTargetedSkillUseEvent_ForSubcommand2E()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.UseTargetedSkill,
            new byte[] { 0x00, 0x0F, 0x00, 0x00, 0x00, 0x07 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<TargetedSkillUseEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.SkillId, Is.EqualTo((ushort)0x000F));
                Assert.That(gameEvent.TargetSerial, Is.EqualTo((Serial)0x00000007u));
            }
        );
    }
}
