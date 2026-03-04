using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Packets.Outgoing.World;

namespace Moongate.Tests.Server.Data.Internal.Entities;

public sealed class LuaItemProxyTests
{
    [Test]
    public void Properties_ShouldMirrorItemEntity()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000001u,
            Name = "door",
            MapId = 1,
            Location = new Point3D(111, 222, 7),
            Amount = 2,
            ItemId = 0x0675,
            Hue = 1109,
            ScriptId = "items.door",
            ParentContainerId = (Serial)0x40000020u,
            ContainerPosition = new Point2D(44, 55),
            Direction = DirectionType.West
        };

        var proxy = new LuaItemProxy(item);

        Assert.Multiple(
            () =>
            {
                Assert.That(proxy.Serial, Is.EqualTo(0x40000001u));
                Assert.That(proxy.Name, Is.EqualTo("door"));
                Assert.That(proxy.MapId, Is.EqualTo(1));
                Assert.That(proxy.LocationX, Is.EqualTo(111));
                Assert.That(proxy.LocationY, Is.EqualTo(222));
                Assert.That(proxy.LocationZ, Is.EqualTo(7));
                Assert.That(proxy.Amount, Is.EqualTo(2));
                Assert.That(proxy.ItemId, Is.EqualTo(0x0675));
                Assert.That(proxy.Hue, Is.EqualTo(1109));
                Assert.That(proxy.ScriptId, Is.EqualTo("items.door"));
                Assert.That(proxy.ParentContainerId, Is.EqualTo(0x40000020u));
                Assert.That(proxy.ContainerX, Is.EqualTo(44));
                Assert.That(proxy.ContainerY, Is.EqualTo(55));
                Assert.That(proxy.Direction, Is.EqualTo(DirectionType.West));
            }
        );
    }

    [Test]
    public void CoreMutations_ShouldPersist()
    {
        var item = CreateItem();
        var itemService = new LuaItemProxyTestItemService();
        var proxy = new LuaItemProxy(item, itemService);

        var setName = proxy.SetName("Renamed");
        var setAmount = proxy.SetAmount(55);
        var addAmount = proxy.AddAmount(5);
        var setHue = proxy.SetHue(1234);
        var setScriptId = proxy.SetScriptId("items.renamed");

        Assert.Multiple(
            () =>
            {
                Assert.That(setName, Is.True);
                Assert.That(setAmount, Is.True);
                Assert.That(addAmount, Is.True);
                Assert.That(setHue, Is.True);
                Assert.That(setScriptId, Is.True);
                Assert.That(item.Name, Is.EqualTo("Renamed"));
                Assert.That(item.Amount, Is.EqualTo(60));
                Assert.That(item.Hue, Is.EqualTo(1234));
                Assert.That(item.ScriptId, Is.EqualTo("items.renamed"));
                Assert.That(itemService.UpsertCalls, Is.EqualTo(5));
            }
        );
    }

    [Test]
    public void PlacementActions_ShouldCallItemService()
    {
        var item = CreateItem();
        var itemService = new LuaItemProxyTestItemService
        {
            MoveToWorldResult = true,
            MoveToContainerResult = true,
            EquipResult = true,
            DeleteResult = true
        };
        var proxy = new LuaItemProxy(item, itemService);

        var world = proxy.MoveToWorld(1, 500, 600, 7);
        var container = proxy.MoveToContainer(0x40000020, 33, 44);
        var equip = proxy.EquipTo(0x00000011, (int)ItemLayerType.Pants);
        var deleted = proxy.Delete();

        Assert.Multiple(
            () =>
            {
                Assert.That(world, Is.True);
                Assert.That(container, Is.True);
                Assert.That(equip, Is.True);
                Assert.That(deleted, Is.True);
                Assert.That(itemService.MoveToWorldCalls, Is.EqualTo(1));
                Assert.That(itemService.MoveToContainerCalls, Is.EqualTo(1));
                Assert.That(itemService.EquipCalls, Is.EqualTo(1));
                Assert.That(itemService.DeleteCalls, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void ScriptActions_ShouldHandlePropsAndSoundAndSpeech()
    {
        var item = CreateItem();
        var itemService = new LuaItemProxyTestItemService();
        var spatial = new LuaItemProxyTestSpatialWorldService();
        var speech = new LuaItemProxyTestSpeechService();
        spatial.PlayersInRange.Add(new GameSession(new(new Moongate.Network.Client.MoongateTCPClient(
            new(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp)
        ))));
        var proxy = new LuaItemProxy(item, itemService, spatial, speech);

        var propInt = proxy.SetProp("charges", 12);
        var propBool = proxy.SetProp("bound", true);
        var propString = proxy.SetProp("owner", "tommy");
        var value = proxy.GetProp("charges");
        var removed = proxy.RemoveProp("charges");
        var sound = proxy.PlaySound(0x006A);
        var recipients = proxy.Say("hello", 12);

        Assert.Multiple(
            () =>
            {
                Assert.That(propInt, Is.True);
                Assert.That(propBool, Is.True);
                Assert.That(propString, Is.True);
                Assert.That(value, Is.EqualTo(12L));
                Assert.That(removed, Is.True);
                Assert.That(sound, Is.True);
                Assert.That(recipients, Is.EqualTo(1));
                Assert.That(spatial.BroadcastCalls, Is.EqualTo(1));
                Assert.That(spatial.LastPacket, Is.TypeOf<PlaySoundEffectPacket>());
                Assert.That(speech.SendCalls, Is.EqualTo(1));
            }
        );
    }

    private static UOItemEntity CreateItem()
    {
        return new()
        {
            Id = (Serial)0x40000001u,
            Name = "door",
            MapId = 1,
            Location = new Point3D(111, 222, 7),
            Amount = 2,
            ItemId = 0x0675,
            Hue = 1109,
            ScriptId = "items.door",
            ParentContainerId = (Serial)0x40000020u,
            ContainerPosition = new Point2D(44, 55),
            Direction = DirectionType.West
        };
    }

    private sealed class LuaItemProxyTestItemService : IItemService
    {
        public int UpsertCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int MoveToWorldCalls { get; private set; }
        public int MoveToContainerCalls { get; private set; }
        public int EquipCalls { get; private set; }
        public bool DeleteResult { get; set; } = true;
        public bool MoveToWorldResult { get; set; } = true;
        public bool MoveToContainerResult { get; set; } = true;
        public bool EquipResult { get; set; } = true;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true) => item;
        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true) => Task.FromResult<UOItemEntity?>(null);
        public Task<Serial> CreateItemAsync(UOItemEntity item) => Task.FromResult((Serial)1u);
        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId) => Task.FromResult(new UOItemEntity());
        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeleteCalls++;
            return Task.FromResult(DeleteResult);
        }
        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult<Moongate.Server.Data.Items.DropItemToGroundResult?>(null);
        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
        {
            EquipCalls++;
            return Task.FromResult(EquipResult);
        }
        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY) => Task.FromResult(new List<UOItemEntity>());
        public Task<UOItemEntity?> GetItemAsync(Serial itemId) => Task.FromResult<UOItemEntity?>(null);
        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId) => Task.FromResult((false, (UOItemEntity?)null));
        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId) => Task.FromResult(new List<UOItemEntity>());
        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            MoveToContainerCalls++;
            return Task.FromResult(MoveToContainerResult);
        }
        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            MoveToWorldCalls++;
            return Task.FromResult(MoveToWorldResult);
        }
        public Task UpsertItemAsync(UOItemEntity item)
        {
            UpsertCalls++;
            return Task.CompletedTask;
        }
        public Task UpsertItemsAsync(params UOItemEntity[] items) => Task.CompletedTask;
    }

    private sealed class LuaItemProxyTestSpatialWorldService : ISpatialWorldService
    {
        public int BroadcastCalls { get; private set; }
        public IGameNetworkPacket? LastPacket { get; private set; }
        public List<GameSession> PlayersInRange { get; } = [];

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = mapId;
            _ = location;
            _ = range;
            _ = excludeSessionId;
            BroadcastCalls++;
            LastPacket = packet;
            return Task.FromResult(1);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }
        public void AddOrUpdateMobile(UOMobileEntity mobile) { }
        public void AddRegion(JsonRegion region) { }
        public JsonRegion? GetRegionById(int regionId) => null;
        public int GetMusic(int mapId, Point3D location) => 0;
        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId) => [];
        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId) => [];
        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
        {
            _ = location;
            _ = range;
            _ = mapId;
            _ = excludeSession;
            return PlayersInRange;
        }
        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY) => [];
        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2) => [];
        public List<MapSector> GetActiveSectors() => [];
        public MapSector? GetSectorByLocation(int mapId, Point3D location) => null;
        public SectorSystemStats GetStats() => new();
        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }
        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }
        public void RemoveEntity(Serial serial) { }
    }

    private sealed class LuaItemProxyTestSpeechService : ISpeechService
    {
        public int SendCalls { get; private set; }

        public Task<int> BroadcastFromServerAsync(string text, short hue = SpeechHues.System, short font = SpeechHues.DefaultFont, string language = "ENU")
            => Task.FromResult(0);
        public Task HandleOpenChatWindowAsync(GameSession session, OpenChatWindowPacket packet, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(GameSession session, UnicodeSpeechPacket speechPacket, CancellationToken cancellationToken = default)
            => Task.FromResult<UnicodeSpeechMessagePacket?>(null);
        public Task<bool> SendMessageFromServerAsync(GameSession session, string text, short hue = SpeechHues.System, short font = SpeechHues.DefaultFont, string language = "ENU")
        {
            _ = session;
            _ = text;
            _ = hue;
            _ = font;
            _ = language;
            SendCalls++;
            return Task.FromResult(true);
        }
        public Task<int> SpeakAsMobileAsync(UOMobileEntity speaker, string text, int range = 12, ChatMessageType messageType = ChatMessageType.Regular, short hue = SpeechHues.Default, short font = SpeechHues.DefaultFont, string language = "ENU", CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
