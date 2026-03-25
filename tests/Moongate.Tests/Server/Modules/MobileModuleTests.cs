using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using MoonSharp.Interpreter;
using Stat = Moongate.UO.Data.Types.Stat;

namespace Moongate.Tests.Server.Modules;

public class MobileModuleTests
{
    [SetUp]
    public void SetUp()
        => SkillInfo.Table =
        [
            new(
                (int)UOSkillName.Archery,
                "Archery",
                0,
                100,
                0,
                "Archer",
                0,
                0,
                0,
                1,
                "Archery",
                Stat.Dexterity,
                Stat.Strength
            ),
            new(
                (int)UOSkillName.Stealing,
                "Stealing",
                0,
                100,
                0,
                "Thief",
                0,
                0,
                0,
                1,
                "Stealing",
                Stat.Dexterity,
                Stat.Intelligence
            ),
            new(
                (int)UOSkillName.Swords,
                "Swords",
                100,
                0,
                0,
                "Swordsman",
                0,
                0,
                0,
                1,
                "Swords",
                Stat.Strength,
                Stat.Dexterity
            )
        ];

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

    private sealed class MobileModuleTestItemService : IItemService
    {
        public UOItemEntity? SpawnedItem { get; set; }

        public UOItemEntity? LastUpsertedItem { get; private set; }

        public Serial LastDeletedItemId { get; private set; } = Serial.Zero;

        public Serial LastMoveItemId { get; private set; } = Serial.Zero;

        public Serial LastContainerId { get; private set; } = Serial.Zero;

        public Point2D LastContainerPosition { get; private set; } = Point2D.Zero;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            LastDeletedItemId = itemId;

            return Task.FromResult(true);
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(SpawnedItem?.Id == itemId ? SpawnedItem : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            LastMoveItemId = itemId;
            LastContainerId = containerId;
            LastContainerPosition = position;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(
                SpawnedItem ??=
                    new UOItemEntity
                    {
                        Id = (Serial)0x800,
                        Name = itemTemplateId,
                        ItemId = 0x0F3F,
                        Amount = 1,
                        IsStackable = true,
                        MapId = 0,
                        Location = Point3D.Zero
                    }
            );

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((SpawnedItem?.Id == itemId, SpawnedItem));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            LastUpsertedItem = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
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
    public void Teleport_WhenCharacterExists_ShouldUpdateMapAndLocation()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x220,
            Name = "Traveler",
            MapId = 1,
            Location = new(100, 200, 5)
        };
        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = mobile
        };
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService()
        );

        var teleported = module.Teleport(0x220, 0, 1496, 1628, 20);

        Assert.Multiple(
            () =>
            {
                Assert.That(teleported, Is.True);
                Assert.That(mobile.MapId, Is.EqualTo(0));
                Assert.That(mobile.Location, Is.EqualTo(new Point3D(1496, 1628, 20)));
            }
        );
    }

    [Test]
    public void Get_WhenCharacterIsMounted_ShouldExposeMountedState()
    {
        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = new()
            {
                Id = (Serial)0x201,
                Name = "Mounted",
                MapId = 1,
                Location = new(100, 200, 0),
                MountedMobileId = (Serial)0x555
            }
        };
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService()
        );

        var reference = module.Get(0x201);

        Assert.That(reference!.IsMounted, Is.True);
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
    public void GetSkill_WhenCharacterHasSkill_ShouldReturnDisplayedSkillValue()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x210,
            Name = "Skilled",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        mobile.InitializeSkills();
        mobile.SetSkill(UOSkillName.Archery, 625);

        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = mobile
        };
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService()
        );

        var skillValue = module.GetSkill(0x210, "archery");

        Assert.That(skillValue, Is.EqualTo(62.5).Within(0.001));
    }

    [Test]
    public void CheckSkill_WhenSkillIsWithinRange_ShouldReturnSuccessAndApplyGain()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x211,
            Name = "Archer",
            IsPlayer = true,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        mobile.InitializeSkills();
        mobile.SetSkill(UOSkillName.Archery, 500);

        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = mobile
        };
        ISkillGainService skillGainService = new SkillGainService(() => 0.0);
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService(),
            skillGainService: skillGainService,
            skillCheckRollProvider: () => 0.0
        );

        var succeeded = module.CheckSkill(0x211, "archery", 0.0, 100.0, 0x999);

        Assert.Multiple(
            () =>
            {
                Assert.That(succeeded, Is.True);
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Base, Is.EqualTo(501));
            }
        );
    }

    [Test]
    public void GetWeapon_WhenCharacterHasEquippedWeapon_ShouldReturnWeaponProxy()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x212,
            Name = "Warrior",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var sword = new UOItemEntity
        {
            Id = (Serial)0x500,
            Name = "Longsword",
            ItemId = 0x0F61,
            WeaponSkill = UOSkillName.Swords,
            MapId = 1,
            Location = mobile.Location,
            CombatStats = new()
            {
                DamageMin = 10,
                DamageMax = 15,
                RangeMin = 0,
                RangeMax = 1
            }
        };
        mobile.AddEquippedItem(ItemLayerType.OneHanded, sword);

        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = mobile
        };
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService()
        );

        var weapon = module.GetWeapon(0x212);

        Assert.Multiple(
            () =>
            {
                Assert.That(weapon, Is.Not.Null);
                Assert.That(weapon!.WeaponSkill, Is.EqualTo("Swords"));
                Assert.That(weapon.RangeMax, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void GetBackpack_WhenCharacterHasBackpack_ShouldReturnBackpackProxy()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x213,
            Name = "Packy",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x600,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = mobile.Location
        };
        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.BackpackId = backpack.Id;

        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = mobile
        };
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService()
        );

        var resolvedBackpack = module.GetBackpack(0x213);

        Assert.That(resolvedBackpack!.Serial, Is.EqualTo(0x600));
    }

    [Test]
    public void ConsumeItem_WhenMatchingItemExistsInQuiver_ShouldConsumeQuiverBeforeBackpack()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x214,
            Name = "Ranger",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x610,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = mobile.Location
        };
        var quiver = new UOItemEntity
        {
            Id = (Serial)0x611,
            Name = "Quiver",
            ItemId = 0x1B02,
            MapId = 1,
            Location = mobile.Location,
            IsQuiver = true
        };
        var quiverArrow = new UOItemEntity
        {
            Id = (Serial)0x612,
            Name = "Arrow",
            ItemId = 0x0F3F,
            Amount = 3,
            IsStackable = true,
            MapId = 1,
            Location = mobile.Location
        };
        var backpackArrow = new UOItemEntity
        {
            Id = (Serial)0x613,
            Name = "Arrow",
            ItemId = 0x0F3F,
            Amount = 8,
            IsStackable = true,
            MapId = 1,
            Location = mobile.Location
        };
        quiver.AddItem(quiverArrow, new(1, 1));
        backpack.AddItem(backpackArrow, new(1, 1));
        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.AddEquippedItem(ItemLayerType.Cloak, quiver);
        mobile.BackpackId = backpack.Id;

        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = mobile
        };
        var itemService = new MobileModuleTestItemService();
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService(),
            itemService: itemService
        );

        var consumed = module.ConsumeItem(0x214, 0x0F3F, 1);

        Assert.Multiple(
            () =>
            {
                Assert.That(consumed, Is.True);
                Assert.That(quiverArrow.Amount, Is.EqualTo(2));
                Assert.That(backpackArrow.Amount, Is.EqualTo(8));
            }
        );
    }

    [Test]
    public void AddItemToBackpack_WhenCharacterHasBackpack_ShouldSpawnAndMoveItem()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x215,
            Name = "Gatherer",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x620,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = mobile.Location
        };
        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.BackpackId = backpack.Id;

        var characterService = new MobileModuleTestCharacterService
        {
            CharacterToReturn = mobile
        };
        var itemService = new MobileModuleTestItemService
        {
            SpawnedItem = new()
            {
                Id = (Serial)0x621,
                Name = "Arrow",
                ItemId = 0x0F3F,
                Amount = 1,
                IsStackable = true,
                MapId = 1,
                Location = Point3D.Zero
            }
        };
        var module = new MobileModule(
            characterService,
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService(),
            itemService: itemService
        );

        var addedItem = module.AddItemToBackpack(0x215, "arrow", 5);

        Assert.Multiple(
            () =>
            {
                Assert.That(addedItem, Is.Not.Null);
                Assert.That(itemService.LastMoveItemId, Is.EqualTo((Serial)0x621));
                Assert.That(itemService.LastContainerId, Is.EqualTo((Serial)0x620));
                Assert.That(itemService.SpawnedItem!.Amount, Is.EqualTo(5));
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

    [Test]
    public void SearchTemplates_WhenQueryMatchesTemplateIdPrefix_ShouldReturnStableMobileMetadata()
    {
        var templateService = new MobileTemplateService();
        templateService.UpsertRange(
        [
            CreateTemplate("zombie_archer", "Zombie Archer"),
            CreateTemplate("zombie_warrior", "Zombie Warrior"),
            CreateTemplate("town_guard", "Town Guard")
        ]
        );
        var module = new MobileModule(
            new MobileModuleTestCharacterService(),
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService(),
            mobileTemplateService: templateService
        );

        var results = module.SearchTemplates("zom");

        Assert.Multiple(
            () =>
            {
                Assert.That(results.Length, Is.EqualTo(2));
                AssertResult(results, 1, "zombie_archer", "Zombie Archer");
                AssertResult(results, 2, "zombie_warrior", "Zombie Warrior");
            }
        );
    }

    [Test]
    public void SearchTemplates_WhenQueryMatchesDisplayNameSubstring_ShouldReturnSubstringMatches()
    {
        var templateService = new MobileTemplateService();
        templateService.UpsertRange(
        [
            CreateTemplate("docks_escort", "Docks Escort"),
            CreateTemplate("harbor_mage", "Harbor Spellbinder"),
            CreateTemplate("forest_wolf", "Forest Wolf")
        ]
        );
        var module = new MobileModule(
            new MobileModuleTestCharacterService(),
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService(),
            mobileTemplateService: templateService
        );

        var results = module.SearchTemplates("spell");

        Assert.Multiple(
            () =>
            {
                Assert.That(results.Length, Is.EqualTo(1));
                AssertResult(results, 1, "harbor_mage", "Harbor Spellbinder");
            }
        );
    }

    [Test]
    public void SearchTemplates_WhenPageSizeExceedsMax_ShouldClampResultCount()
    {
        var templateService = new MobileTemplateService();

        for (var index = 1; index <= 60; index++)
        {
            templateService.Upsert(CreateTemplate($"search_mobile_{index:00}", $"Search Mobile {index:00}"));
        }

        var module = new MobileModule(
            new MobileModuleTestCharacterService(),
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService(),
            mobileTemplateService: templateService
        );

        var results = module.SearchTemplates("search_mobile", pageSize: 999);

        Assert.Multiple(
            () =>
            {
                Assert.That(results.Length, Is.EqualTo(50));
                AssertResult(results, 1, "search_mobile_01", "Search Mobile 01");
                AssertResult(results, 50, "search_mobile_50", "Search Mobile 50");
            }
        );
    }

    private static void AssertResult(Table results, int index, string templateId, string displayName)
    {
        var entry = results.Get(index).Table;

        Assert.That(entry, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(entry!.Get("template_id").String, Is.EqualTo(templateId));
                Assert.That(entry.Get("display_name").String, Is.EqualTo(displayName));
            }
        );
    }

    private static MobileTemplateDefinition CreateTemplate(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            Category = "Test",
            Description = name,
            Title = string.Empty
        };
}
