using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Spans;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class CharacterHandlerTests
{
    [Test]
    public async Task HandleCharacterLoggedIn_ShouldPublishPlayerCharacterLoggedInEvent()
    {
        EnsureMapRegistered();

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new MovementHandlerTestCharacterService();
        var entityFactoryService = new CharacterHandlerTestEntityFactoryService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var handler = new CharacterHandler(
            queue,
            characterService,
            entityFactoryService,
            eventBus,
            gameNetworkSessionService,
            new RegionDataLoaderTestSpatialWorldService()
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304,
            AccountType = AccountType.Regular
        };

        var characterId = (Serial)0x00000099;

        var result = await handler.HandleCharacterLoggedIn(session, characterId);
        var gameEvent = eventBus.Events.OfType<PlayerCharacterLoggedInEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.AccountId, Is.EqualTo(session.AccountId));
                Assert.That(gameEvent.CharacterId, Is.EqualTo(characterId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_CharacterCreation_ShouldApplyStarterEquipmentHuesFromPacket()
    {
        EnsureMapRegistered();

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new MovementHandlerTestCharacterService();
        var entityFactoryService = new CharacterHandlerTestEntityFactoryService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var handler = new CharacterHandler(
            queue,
            characterService,
            entityFactoryService,
            eventBus,
            gameNetworkSessionService,
            new RegionDataLoaderTestSpatialWorldService()
        );

        var packet = new CharacterCreationPacket();
        var parsed = packet.TryParse(BuildCharacterCreationPayload());
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304,
            AccountType = AccountType.Regular
        };

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(handled, Is.True);
                Assert.That(characterService.ApplyStarterEquipmentHuesCalls, Is.EqualTo(1));
                Assert.That(characterService.LastAppliedCharacterId, Is.EqualTo((Serial)1u));
                Assert.That(characterService.LastAppliedShirtHue, Is.EqualTo(packet.Shirt.Hue));
                Assert.That(characterService.LastAppliedPantsHue, Is.EqualTo(packet.Pants.Hue));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_MobileDoubleClick_ShouldPublishMobileDoubleClickEvent()
    {
        EnsureMapRegistered();

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new MovementHandlerTestCharacterService();
        var entityFactoryService = new CharacterHandlerTestEntityFactoryService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var handler = new CharacterHandler(
            queue,
            characterService,
            entityFactoryService,
            eventBus,
            gameNetworkSessionService,
            new RegionDataLoaderTestSpatialWorldService()
        );

        var packet = new DoubleClickPacket
        {
            TargetSerial = (Serial)0x00000099u
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304,
            AccountType = AccountType.Regular
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var gameEvent = eventBus.Events.OfType<MobileDoubleClickEvent>().SingleOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.MobileSerial, Is.EqualTo((Serial)0x00000099u));
            }
        );
    }

    private static void EnsureMapRegistered()
    {
        if (Map.GetMap(0) is null)
        {
            _ = Map.RegisterMap(
                0,
                0,
                0,
                6144,
                4096,
                SeasonType.Summer,
                "Felucca",
                MapRules.FeluccaRules
            );
        }
    }

    private static byte[] BuildCharacterCreationPayload()
    {
        var writer = new SpanWriter(106, true);

        writer.Write((byte)0xF8);
        writer.Write(unchecked((int)0xEDEDEDED));
        writer.Write(unchecked((int)0xFFFFFFFF));
        writer.Write((byte)0x00);
        writer.WriteAscii("TestCharacter", 30);
        writer.Write((ushort)0);
        writer.Write((uint)ClientFlags.Trammel);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)2);
        writer.Clear(15);
        writer.Write((byte)5);
        writer.Write((byte)60);
        writer.Write((byte)50);
        writer.Write((byte)40);
        writer.Write((byte)0);
        writer.Write((byte)50);
        writer.Write((byte)1);
        writer.Write((byte)50);
        writer.Write((byte)2);
        writer.Write((byte)50);
        writer.Write((byte)3);
        writer.Write((byte)50);
        writer.Write((short)0x0455);
        writer.Write((short)0x0203);
        writer.Write((short)0x0304);
        writer.Write((short)0x0506);
        writer.Write((short)0x0708);
        writer.Write((short)3);
        writer.Write((ushort)0);
        writer.Write((short)1);
        writer.Write(0);
        writer.Write((short)0x0888);
        writer.Write((short)0x0999);

        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }
}
