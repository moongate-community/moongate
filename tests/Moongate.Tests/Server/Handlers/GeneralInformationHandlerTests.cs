using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Data.Events.Party;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Version;

namespace Moongate.Tests.Server.Handlers;

public class GeneralInformationHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_ShouldIgnoreSubcommand0E_WhenActionIsInvalid()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000002u,
                MapId = 1,
                Location = new(120, 130, 0)
            }
        };
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.Action3DClient,
            new byte[] { 0x00, 0x00, 0x00, 0x16 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<MobilePlayAnimationEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishContextMenuEntrySelectedEvent_ForSubcommand15()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        var targetSerial = (Serial)0x00000009u;
        const ushort entryTag = 3;
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.PopupEntrySelection,
            new byte[] { 0x00, 0x00, 0x00, 0x09, 0x00, 0x03 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<ContextMenuEntrySelectedEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.TargetSerial, Is.EqualTo(targetSerial));
                Assert.That(gameEvent.EntryTag, Is.EqualTo(entryTag));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishContextMenuRequestedEvent_ForSubcommand13()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        var targetSerial = (Serial)0x00000002u;
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.RequestPopupMenu,
            new byte[] { 0x00, 0x00, 0x00, 0x02 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<ContextMenuRequestedEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.TargetSerial, Is.EqualTo(targetSerial));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishMobilePlayAnimationEvent_ForSubcommand0EAndValidAction()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000002u,
                MapId = 1,
                Location = new(120, 130, 0)
            }
        };
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.Action3DClient,
            new byte[] { 0x00, 0x00, 0x00, 0x20 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<MobilePlayAnimationEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.MobileId, Is.EqualTo((Serial)0x00000002u));
                Assert.That(gameEvent.MapId, Is.EqualTo(1));
                Assert.That(gameEvent.Location, Is.EqualTo(new Point3D(120, 130, 0)));
                Assert.That(gameEvent.Action, Is.EqualTo((short)32));
            }
        );
    }

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
    public async Task HandlePacketAsync_ShouldPublishSpellCastRequestedEvent_ForSubcommand1C()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.SpellSelected,
            new byte[] { 0x00, 0x02, 0x00, 0x2D }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<SpellCastRequestedEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.SpellId, Is.EqualTo((ushort)0x002D));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishSpellCastRequestedEvent_ForLegacySubcommand1C()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.SpellSelected,
            new byte[] { 0x00, 0x2D }
        );

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<SpellCastRequestedEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.SpellId, Is.EqualTo((ushort)0x002D));
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
    public async Task HandlePacketAsync_ShouldStoreEnhancedClientType_ForClientTypeSubcommand()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new GeneralInformationHandler(new BasePacketListenerTestOutgoingPacketQueue(), eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = GeneralInformationPacket.Create(
            GeneralInformationSubcommandType.ClientType,
            new byte[] { 0x00, 0x00, 0x00, 0x03, 0x00 }
        );

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(session.NetworkSession.ClientType, Is.EqualTo(ClientType.SA));
                Assert.That(session.NetworkSession.IsEnhancedClient, Is.True);
            }
        );
    }
}
