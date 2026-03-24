using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using MoonSharp.Interpreter;

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

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = packet;
            _ = cancellationToken;

            return Task.CompletedTask;
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

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = SpeechHues.Default,
            short font = SpeechHues.DefaultFont,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
        {
            _ = speaker;
            _ = text;
            _ = range;
            _ = messageType;
            _ = hue;
            _ = font;
            _ = language;
            _ = cancellationToken;

            return Task.FromResult(0);
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

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            _ = characterId;
            _ = shirtHue;
            _ = pantsHue;

            return Task.CompletedTask;
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

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
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

    private sealed class MobileModuleTestMobileService : IMobileService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();
        public List<Serial> CreateOrUpdateCalls { get; } = [];

        public Serial LastRiderId { get; private set; } = Serial.Zero;
        public Serial LastMountId { get; private set; } = Serial.Zero;
        public int DismountCalls { get; private set; }
        public string? LastSpawnTemplateId { get; private set; }
        public Point3D LastSpawnLocation { get; private set; } = Point3D.Zero;
        public int LastSpawnMapId { get; private set; }

        public UOMobileEntity? SpawnedMobile
        {
            get => _spawnedMobile;
            set
            {
                _spawnedMobile = value;

                if (value is not null)
                {
                    _mobiles[value.Id] = value;
                }
            }
        }

        private UOMobileEntity? _spawnedMobile;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            CreateOrUpdateCalls.Add(mobile.Id);
            _mobiles[mobile.Id] = mobile;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> DismountAsync(Serial riderId, CancellationToken cancellationToken = default)
        {
            LastRiderId = riderId;
            DismountCalls++;

            if (_mobiles.TryGetValue(riderId, out var rider))
            {
                var mountId = rider.MountedMobileId;
                rider.MountedMobileId = Serial.Zero;
                rider.MountedDisplayItemId = 0;

                if (mountId != Serial.Zero && _mobiles.TryGetValue(mountId, out var mount))
                {
                    mount.RiderMobileId = Serial.Zero;
                }
            }

            return Task.FromResult(true);
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(_mobiles.GetValueOrDefault(id));

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(new List<UOMobileEntity>());

        public void Register(UOMobileEntity mobile)
            => _mobiles[mobile.Id] = mobile;

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
        {
            _ = accountId;
            _ = cancellationToken;

            LastSpawnTemplateId = templateId;
            LastSpawnLocation = location;
            LastSpawnMapId = mapId;

            return Task.FromResult(
                SpawnedMobile ??
                new UOMobileEntity
                {
                    Id = (Serial)0x400,
                    Name = "Horse",
                    MapId = mapId,
                    Location = location
                }
            );
        }

        public Task<bool> TryMountAsync(Serial riderId, Serial mountId, CancellationToken cancellationToken = default)
        {
            LastRiderId = riderId;
            LastMountId = mountId;

            if (_mobiles.TryGetValue(riderId, out var rider) && _mobiles.TryGetValue(mountId, out var mount))
            {
                rider.MountedMobileId = mountId;
                mount.RiderMobileId = riderId;
            }

            return Task.FromResult(true);
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    [Test]
    public void Dismount_ShouldDelegateToMobileService()
    {
        var characterService = new MobileModuleTestCharacterService();
        var speechService = new MobileModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var mobileService = new MobileModuleTestMobileService();
        var module = new MobileModule(
            characterService,
            speechService,
            sessionService,
            spatialService,
            mobileService: mobileService
        );

        var dismounted = module.Dismount(0x200);

        Assert.Multiple(
            () =>
            {
                Assert.That(dismounted, Is.True);
                Assert.That(mobileService.LastRiderId, Is.EqualTo((Serial)0x200));
                Assert.That(mobileService.DismountCalls, Is.EqualTo(1));
            }
        );
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

    [Test]
    public void Spawn_WhenPositionIsValid_ShouldDelegateToMobileService()
    {
        var characterService = new MobileModuleTestCharacterService();
        var speechService = new MobileModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var mobileService = new MobileModuleTestMobileService();
        var module = new MobileModule(
            characterService,
            speechService,
            sessionService,
            spatialService,
            mobileService: mobileService
        );
        var position = new Table(null);
        position["x"] = 100;
        position["y"] = 200;
        position["z"] = 5;
        position["map_id"] = 1;

        var mobile = module.Spawn("ethereal_horse_mount", position);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile, Is.Not.Null);
                Assert.That(mobileService.LastSpawnTemplateId, Is.EqualTo("ethereal_horse_mount"));
                Assert.That(mobileService.LastSpawnLocation, Is.EqualTo(new Point3D(100, 200, 5)));
                Assert.That(mobileService.LastSpawnMapId, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void TryMount_ShouldDelegateToMobileService()
    {
        var characterService = new MobileModuleTestCharacterService();
        var speechService = new MobileModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var mobileService = new MobileModuleTestMobileService();
        var module = new MobileModule(
            characterService,
            speechService,
            sessionService,
            spatialService,
            mobileService: mobileService
        );

        var mounted = module.TryMount(0x200, 0x300);

        Assert.Multiple(
            () =>
            {
                Assert.That(mounted, Is.True);
                Assert.That(mobileService.LastRiderId, Is.EqualTo((Serial)0x200));
                Assert.That(mobileService.LastMountId, Is.EqualTo((Serial)0x300));
            }
        );
    }

    [Test]
    public void TryMount_ShouldPersistRuntimeRiderAndMountBeforeDelegating()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var characterService = new MobileModuleTestCharacterService();
        var speechService = new MobileModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var mobileService = new MobileModuleTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var rider = new UOMobileEntity
        {
            Id = (Serial)0x200,
            Name = "Rider",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var mount = new UOMobileEntity
        {
            Id = (Serial)0x300,
            Name = "Horse",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        mobileService.Register(rider);
        mobileService.Register(mount);
        spatialService.AddOrUpdateMobile(mount);
        var session = new GameSession(new(client))
        {
            CharacterId = rider.Id,
            Character = rider
        };
        sessionService.Add(session);
        var module = new MobileModule(
            characterService,
            speechService,
            sessionService,
            spatialService,
            mobileService: mobileService,
            outgoingPacketQueue: outgoing
        );

        var mounted = module.TryMount(0x200, 0x300);

        Assert.Multiple(
            () =>
            {
                Assert.That(mounted, Is.True);
                Assert.That(mobileService.CreateOrUpdateCalls, Is.EqualTo([(Serial)0x200, (Serial)0x300]));
                Assert.That(mobileService.LastRiderId, Is.EqualTo((Serial)0x200));
                Assert.That(mobileService.LastMountId, Is.EqualTo((Serial)0x300));
            }
        );
    }

    [Test]
    public void TryMount_ShouldUpdateSessionMountedState_AndEnqueueSelfRefresh()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var characterService = new MobileModuleTestCharacterService();
        var speechService = new MobileModuleTestSpeechService();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var mobileService = new MobileModuleTestMobileService
        {
            SpawnedMobile = new()
            {
                Id = (Serial)0x300,
                Name = "horse",
                MapId = 1,
                Location = new(100, 100, 0)
            }
        };
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var rider = new UOMobileEntity
        {
            Id = (Serial)0x200,
            Name = "rider",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        spatialService.AddOrUpdateMobile(rider);
        spatialService.AddOrUpdateMobile(mobileService.SpawnedMobile);
        var session = new GameSession(new(client))
        {
            CharacterId = rider.Id,
            Character = rider
        };
        sessionService.Add(session);
        var module = new MobileModule(
            characterService,
            speechService,
            sessionService,
            spatialService,
            mobileService: mobileService,
            outgoingPacketQueue: queue
        );

        var mounted = module.TryMount(0x200, 0x300);

        Assert.Multiple(
            () =>
            {
                Assert.That(mounted, Is.True);
                Assert.That(session.IsMounted, Is.True);
                Assert.That(session.Character!.MountedMobileId, Is.EqualTo((Serial)0x300));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DrawPlayerPacket>());
            }
        );
    }
}
