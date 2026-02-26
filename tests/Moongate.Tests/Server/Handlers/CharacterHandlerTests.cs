using System.Net.Sockets;
using Moongate.Network.Client;
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
}
