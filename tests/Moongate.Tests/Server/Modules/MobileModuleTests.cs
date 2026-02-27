using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Support;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Modules;

public class MobileModuleTests
{
    private sealed class MobileModuleTestSpeechService : ISpeechService
    {
        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            _ = text;
            _ = hue;
            _ = font;
            _ = language;

            return Task.FromResult(0);
        }

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = speechPacket;
            _ = cancellationToken;

            return Task.FromResult<UnicodeSpeechMessagePacket?>(null);
        }

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            _ = session;
            _ = text;
            _ = hue;
            _ = font;
            _ = language;

            return Task.FromResult(true);
        }
    }

    private sealed class MobileModuleTestCharacterService : ICharacterService
    {
        public UOMobileEntity? CharacterToReturn { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult((Serial)1u);
        }

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            _ = characterId;

            return Task.FromResult(CharacterToReturn);
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            _ = accountId;

            return Task.FromResult(new List<UOMobileEntity>());
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }
    }

    [Test]
    public void Get_WhenCharacterDoesNotExist_ShouldReturnNull()
    {
        var characterService = new MobileModuleTestCharacterService();
        var speechService = new MobileModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var module = new MobileModule(characterService, speechService, sessionService, spatialService);

        var reference = module.Get(0x201);

        Assert.That(reference, Is.Null);
    }

    [Test]
    public void Get_WhenCharacterExists_ShouldReturnLuaMobileProxy()
    {
        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = new()
            {
                Id = (Serial)0x200,
                Name = "TestMobile",
                MapId = 1,
                Location = new(100, 200, 5)
            }
        };
        var speechService = new MobileModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var module = new MobileModule(characterService, speechService, sessionService, spatialService);

        var reference = module.Get(0x200);

        Assert.Multiple(
            () =>
            {
                Assert.That(reference, Is.Not.Null);
                Assert.That(reference!.Serial, Is.EqualTo(0x200));
                Assert.That(reference.Name, Is.EqualTo("TestMobile"));
                Assert.That(reference.MapId, Is.EqualTo(1));
                Assert.That(reference.LocationX, Is.EqualTo(100));
                Assert.That(reference.LocationY, Is.EqualTo(200));
                Assert.That(reference.LocationZ, Is.EqualTo(5));
            }
        );
    }
}
