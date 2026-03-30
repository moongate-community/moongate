using System.Reflection;
using Moongate.Server.Data.Items;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class MagicModuleTests
{
    private sealed class RecordingMagicService : IMagicService
    {
        public bool IsCastingResult { get; set; }

        public Serial? LastCasterId { get; private set; }

        public int? LastSpellId { get; private set; }

        public SpellTargetData? LastTarget { get; private set; }

        public int IsCastingCalls { get; private set; }

        public int InterruptCalls { get; private set; }

        public int TryCastCalls { get; private set; }

        public bool TryCastResult { get; set; } = true;

        public bool TrySetTargetResult { get; set; } = true;

        public bool IsCasting(Serial casterId)
        {
            LastCasterId = casterId;
            IsCastingCalls++;

            return IsCastingResult;
        }

        public bool TrySetTarget(Serial casterId, int spellId, Serial targetId)
        {
            LastCasterId = casterId;
            LastSpellId = spellId;
            LastTarget = SpellTargetData.Mobile(targetId);

            return TrySetTargetResult;
        }

        public ValueTask<bool> TrySetTargetAsync(
            Serial casterId,
            int spellId,
            SpellTargetData target,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastCasterId = casterId;
            LastSpellId = spellId;
            LastTarget = target;

            return ValueTask.FromResult(TrySetTargetResult);
        }

        public ValueTask<bool> TryCastAsync(
            UOMobileEntity caster,
            int spellId,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastCasterId = caster.Id;
            LastSpellId = spellId;
            TryCastCalls++;

            return ValueTask.FromResult(TryCastResult);
        }

        public void Interrupt(Serial casterId)
        {
            LastCasterId = casterId;
            InterruptCalls++;
        }

        public ValueTask OnCastTimerExpiredAsync(Serial casterId, CancellationToken cancellationToken = default)
        {
            _ = casterId;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class MagicModuleTestSpatialWorldService : ISpatialWorldService
    {
        private readonly List<MapSector> _sectors = [];

        public void AddMobile(UOMobileEntity mobile)
        {
            var sector = new MapSector(
                mobile.MapId,
                mobile.Location.X >> MapSectorConsts.SectorShift,
                mobile.Location.Y >> MapSectorConsts.SectorShift
            );
            sector.AddEntity(mobile);
            _sectors.Add(sector);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => AddMobile(mobile);

        public void AddRegion(JsonRegion region) { }

        public Task<int> BroadcastToPlayersAsync(
            Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
            => Task.FromResult(0);

        public List<MapSector> GetActiveSectors()
            => [.. _sectors];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

        public List<Moongate.Server.Data.Session.GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            Moongate.Server.Data.Session.GameSession? excludeSession = null
        )
            => [];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => [];

        public JsonRegion? GetRegionById(int regionId)
            => null;

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
            => null;

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }
    }

    private sealed class RecordingItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public void Add(UOItemEntity item)
            => _items[item.Id] = item;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => throw new NotSupportedException();

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((_items.ContainsKey(itemId), _items.GetValueOrDefault(itemId)));

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    [Test]
    public void ScriptModule_ShouldExposeExpectedLuaModuleAndFunctions()
    {
        var moduleType = typeof(MagicModule);

        var moduleAttribute = moduleType.GetCustomAttributes(typeof(ScriptModuleAttribute), inherit: false)
                                        .Cast<ScriptModuleAttribute>()
                                        .SingleOrDefault();

        Assert.That(moduleAttribute, Is.Not.Null, "Expected MagicModule to declare ScriptModuleAttribute.");
        Assert.That(moduleAttribute!.Name, Is.EqualTo("magic"));

        Assert.Multiple(
            () =>
            {
                Assert.That(GetScriptFunctionName(moduleType, "Cast"), Is.EqualTo("cast"));
                Assert.That(GetScriptFunctionName(moduleType, "CastItem"), Is.EqualTo("cast_item"));
                Assert.That(GetScriptFunctionName(moduleType, "CastLocation"), Is.EqualTo("cast_location"));
                Assert.That(GetScriptFunctionName(moduleType, "CastMobile"), Is.EqualTo("cast_mobile"));
                Assert.That(GetScriptFunctionName(moduleType, "IsCasting"), Is.EqualTo("is_casting"));
                Assert.That(GetScriptFunctionName(moduleType, "Interrupt"), Is.EqualTo("interrupt"));
            }
        );
    }

    [Test]
    public void IsCasting_AndInterrupt_ShouldDelegateToMagicService()
    {
        var spatial = new MagicModuleTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x501u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        spatial.AddMobile(npc);
        var magicService = new RecordingMagicService
        {
            IsCastingResult = true
        };
        var module = new MagicModule(spatial, magicService, new RecordingItemService());

        var isCasting = module.IsCasting((uint)npc.Id);
        _ = module.Interrupt((uint)npc.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(isCasting, Is.True);
                Assert.That(magicService.IsCastingCalls, Is.EqualTo(1));
                Assert.That(magicService.InterruptCalls, Is.EqualTo(1));
                Assert.That(magicService.LastCasterId, Is.EqualTo(npc.Id));
            }
        );
    }

    [Test]
    public void CastMobile_WhenNpcAndTargetExist_ShouldStartCastAndBindMobileTarget()
    {
        var spatial = new MagicModuleTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x601u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var target = new UOMobileEntity
        {
            Id = (Serial)0x602u,
            MapId = 1,
            Location = new(101, 100, 0)
        };
        spatial.AddMobile(npc);
        spatial.AddMobile(target);
        var magicService = new RecordingMagicService();
        var module = new MagicModule(spatial, magicService, new RecordingItemService());

        var result = module.CastMobile((uint)npc.Id, 40, (uint)target.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(magicService.TryCastCalls, Is.EqualTo(1));
                Assert.That(magicService.LastCasterId, Is.EqualTo(npc.Id));
                Assert.That(magicService.LastSpellId, Is.EqualTo(40));
                Assert.That(magicService.LastTarget?.Kind, Is.EqualTo(SpellTargetKind.Mobile));
                Assert.That(magicService.LastTarget?.TargetId, Is.EqualTo(target.Id));
            }
        );
    }

    [Test]
    public void CastItem_WhenNpcAndItemExist_ShouldStartCastAndBindItemTarget()
    {
        var spatial = new MagicModuleTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x611u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        spatial.AddMobile(npc);
        var itemService = new RecordingItemService();
        var targetItem = new UOItemEntity
        {
            Id = (Serial)0x40000100u,
            ItemId = 0x1F14,
            MapId = 1,
            Location = new(120, 140, 0)
        };
        itemService.Add(targetItem);
        var magicService = new RecordingMagicService();
        var module = new MagicModule(spatial, magicService, itemService);

        var result = module.CastItem((uint)npc.Id, 55, (uint)targetItem.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(magicService.TryCastCalls, Is.EqualTo(1));
                Assert.That(magicService.LastTarget?.Kind, Is.EqualTo(SpellTargetKind.Item));
                Assert.That(magicService.LastTarget?.TargetId, Is.EqualTo(targetItem.Id));
                Assert.That(magicService.LastTarget?.Location, Is.EqualTo(targetItem.Location));
                Assert.That(magicService.LastTarget?.Graphic, Is.EqualTo((ushort)targetItem.ItemId));
            }
        );
    }

    [Test]
    public void CastLocation_WhenNpcExists_ShouldStartCastAndBindLocationTarget()
    {
        var spatial = new MagicModuleTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x621u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        spatial.AddMobile(npc);
        var magicService = new RecordingMagicService();
        var module = new MagicModule(spatial, magicService, new RecordingItemService());

        var result = module.CastLocation((uint)npc.Id, 33, 2, 512, 640, 5);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(magicService.TryCastCalls, Is.EqualTo(1));
                Assert.That(magicService.LastTarget?.Kind, Is.EqualTo(SpellTargetKind.Location));
                Assert.That(magicService.LastTarget?.MapId, Is.EqualTo(2));
                Assert.That(magicService.LastTarget?.Location, Is.EqualTo(new Point3D(512, 640, 5)));
            }
        );
    }

    private static string GetScriptFunctionName(Type moduleType, string methodName)
    {
        var method = moduleType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, $"Expected method {methodName} to exist.");

        if (method is null)
        {
            return string.Empty;
        }

        var attribute = method.GetCustomAttributes(typeof(ScriptFunctionAttribute), inherit: false)
                              .Cast<ScriptFunctionAttribute>()
                              .SingleOrDefault();

        Assert.That(attribute, Is.Not.Null, $"Expected {methodName} to declare ScriptFunctionAttribute.");

        return attribute?.FunctionName ?? string.Empty;
    }
}
