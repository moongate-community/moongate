using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Incoming.UI;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Modules;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Scripting;

public sealed class GmMenuLuaRuntimeTests
{
    private sealed class GmMenuLuaRuntimeCharacterService : ICharacterService
    {
        public UOMobileEntity? Character { get; set; }

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
            Character = character;

            return Task.FromResult(character.Id);
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
            => Task.FromResult(Character?.Id == characterId ? Character : null);

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

    private sealed class GmMenuLuaRuntimeLocationCatalogService : ILocationCatalogService
    {
        public IReadOnlyList<WorldLocationEntry> Locations { get; set; } = [];

        public IReadOnlyList<WorldLocationEntry> GetAllLocations()
            => Locations;

        public void SetLocations(IReadOnlyList<WorldLocationEntry> locations)
            => Locations = locations;
    }

    private sealed class GmMenuLuaRuntimeSpatialWorldService : ISpatialWorldService
    {
        public List<UOMobileEntity> AddedOrUpdatedMobiles { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => AddedOrUpdatedMobiles.Add(mobile);

        public void AddRegion(JsonRegion region)
            => _ = region;

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = packet;
            _ = mapId;
            _ = location;
            _ = range;
            _ = excludeSessionId;

            return Task.FromResult(0);
        }

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        {
            _ = mapId;
            _ = centerSectorX;
            _ = centerSectorY;
            _ = radius;

            return [];
        }

        public int GetMusic(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return 0;
        }

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
        {
            _ = location;
            _ = range;
            _ = mapId;
            _ = excludeSession;

            return [];
        }

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            return [];
        }

        public JsonRegion? GetRegionById(int regionId)
        {
            _ = regionId;

            return null;
        }

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return null;
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        {
            _ = item;
            _ = mapId;
            _ = oldLocation;
            _ = newLocation;
        }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
        {
            _ = mobile;
            _ = oldLocation;
            _ = newLocation;
        }

        public void RemoveEntity(Serial serial)
            => _ = serial;

        public JsonRegion? ResolveRegion(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return null;
        }
    }

    private sealed class GmMenuLuaRuntimeSpeechService : ISpeechService
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

    private sealed class GmMenuLuaRuntimeItemTemplateService : IItemTemplateService
    {
        private readonly Dictionary<string, ItemTemplateDefinition> _templates = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _templates.Count;

        public void Clear()
            => _templates.Clear();

        public IReadOnlyList<ItemTemplateDefinition> GetAll()
            => _templates.Values.ToList();

        public bool TryGet(string id, out ItemTemplateDefinition? definition)
            => _templates.TryGetValue(id, out definition);

        public void Upsert(ItemTemplateDefinition definition)
            => _templates[definition.Id] = definition;

        public void UpsertRange(IEnumerable<ItemTemplateDefinition> templates)
        {
            foreach (var template in templates)
            {
                Upsert(template);
            }
        }
    }

    private sealed class GmMenuLuaRuntimeMobileTemplateService : IMobileTemplateService
    {
        private readonly Dictionary<string, MobileTemplateDefinition> _templates = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _templates.Count;

        public void Clear()
            => _templates.Clear();

        public IReadOnlyList<MobileTemplateDefinition> GetAll()
            => _templates.Values.ToList();

        public bool TryGet(string id, out MobileTemplateDefinition? definition)
            => _templates.TryGetValue(id, out definition);

        public void Upsert(MobileTemplateDefinition definition)
            => _templates[definition.Id] = definition;

        public void UpsertRange(IEnumerable<MobileTemplateDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                Upsert(definition);
            }
        }
    }

    private sealed class GmMenuLuaRuntimeItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = new();
        private uint _nextSerial = 0x40002000u;

        public string? LastSpawnTemplateId { get; private set; }

        public UOItemEntity? LastSpawnedItem { get; private set; }

        public Serial LastMoveItemId { get; private set; } = Serial.Zero;

        public Serial LastContainerId { get; private set; } = Serial.Zero;

        public Point2D LastContainerPosition { get; private set; } = Point2D.Zero;

        public Point3D LastWorldLocation { get; private set; } = Point3D.Zero;

        public int LastWorldMapId { get; private set; }

        public UOItemEntity? LastUpsertedItem { get; private set; }

        public void Register(UOItemEntity item)
            => _items[item.Id] = item;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }

            return Task.CompletedTask;
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult(_items.GetValueOrDefault(itemId));

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            _items[item.Id] = item;

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            _items.Remove(itemId);

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
        {
            _ = itemId;
            _ = mobileId;
            _ = layer;

            return Task.FromResult(true);
        }

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(_items.Values.Where(item => item.ParentContainerId == containerId).ToList());

        public Task<bool> MoveItemToContainerAsync(
            Serial itemId,
            Serial containerId,
            Point2D position,
            long sessionId = 0
        )
        {
            LastMoveItemId = itemId;
            LastContainerId = containerId;
            LastContainerPosition = position;

            if (_items.TryGetValue(itemId, out var item))
            {
                item.ParentContainerId = containerId;
                item.ContainerPosition = position;
            }

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            LastMoveItemId = itemId;
            LastWorldLocation = location;
            LastWorldMapId = mapId;

            if (_items.TryGetValue(itemId, out var item))
            {
                item.ParentContainerId = Serial.Zero;
                item.Location = location;
                item.MapId = mapId;
            }

            return Task.FromResult(true);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            LastSpawnTemplateId = itemTemplateId;
            var item = new UOItemEntity
            {
                Id = (Serial)_nextSerial++,
                Name = itemTemplateId,
                ItemId = itemTemplateId.Equals("gold_coin", StringComparison.OrdinalIgnoreCase) ? 0x0EED : 0x1BFB,
                Amount = 1,
                IsStackable = true,
                MapId = 0,
                Location = Point3D.Zero
            };
            _items[item.Id] = item;
            LastSpawnedItem = item;

            return Task.FromResult(item);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((_items.TryGetValue(itemId, out var item), item));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            _items[item.Id] = item;
            LastUpsertedItem = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class GmMenuLuaRuntimeMobileService : IMobileService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();
        private uint _nextSerial = 0x50002000u;

        public List<(string TemplateId, Point3D Location, int MapId)> SpawnCalls { get; } = [];

        public void Register(UOMobileEntity mobile)
            => _mobiles[mobile.Id] = mobile;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _mobiles[mobile.Id] = mobile;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _mobiles.Remove(id);

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

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
        {
            var mobile = new UOMobileEntity
            {
                Id = (Serial)_nextSerial++,
                Name = templateId,
                MapId = mapId,
                Location = location
            };
            _mobiles[mobile.Id] = mobile;
            SpawnCalls.Add((templateId, location, mapId));

            return Task.FromResult(mobile);
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
        {
            var mobile = new UOMobileEntity
            {
                Id = (Serial)_nextSerial++,
                Name = templateId,
                MapId = mapId,
                Location = location
            };
            _mobiles[mobile.Id] = mobile;
            SpawnCalls.Add((templateId, location, mapId));

            return Task.FromResult((true, (UOMobileEntity?)mobile));
        }
    }

    private sealed class GmMenuLuaRuntimePlayerTargetService : IPlayerTargetService
    {
        public long LastSessionId { get; private set; }

        public TargetCursorSelectionType LastSelectionType { get; private set; }

        public TargetCursorType LastCursorType { get; private set; }

        public Action<PendingCursorCallback>? LastCallback { get; private set; }

        public int RequestCount { get; private set; }

        public Serial LastCursorId { get; private set; } = Serial.Zero;

        public long LastCancelSessionId { get; private set; }

        public Serial LastCancelCursorId { get; private set; } = Serial.Zero;

        private uint _nextCursorId = 0x40001000u;

        public Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId)
        {
            LastCancelSessionId = sessionId;
            LastCancelCursorId = cursorId;

            return Task.CompletedTask;
        }

        public void ResolveLocation(int x, int y, int z)
        {
            LastCallback?.Invoke(
                new(
                    new TargetCursorCommandsPacket
                    {
                        CursorTarget = TargetCursorSelectionType.SelectLocation,
                        CursorId = LastCursorId,
                        CursorType = LastCursorType,
                        Location = new Point3D(x, y, z)
                    }
                )
            );
        }

        public void ResolveCancel()
        {
            LastCallback?.Invoke(
                new(
                    new TargetCursorCommandsPacket
                    {
                        CursorTarget = TargetCursorSelectionType.SelectLocation,
                        CursorId = LastCursorId,
                        CursorType = TargetCursorType.CancelCurrentTargeting,
                        Location = Point3D.Zero
                    }
                )
            );
        }

        public Task<Serial> SendTargetCursorAsync(
            long sessionId,
            Action<PendingCursorCallback> callback,
            TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
            TargetCursorType cursorType = TargetCursorType.Neutral
        )
        {
            LastSessionId = sessionId;
            LastSelectionType = selectionType;
            LastCursorType = cursorType;
            LastCallback = callback;
            RequestCount++;
            LastCursorId = (Serial)_nextCursorId++;

            return Task.FromResult(LastCursorId);
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class GmMenuRuntimeContext : IDisposable
    {
        public GmMenuRuntimeContext(
            TempDirectory tempDirectory,
            LuaScriptEngineService service,
            BasePacketListenerTestOutgoingPacketQueue queue,
            FakeGameNetworkSessionService sessionService,
            GumpScriptDispatcherService gumpDispatcher,
            GmMenuLuaRuntimeItemTemplateService itemTemplateService,
            GmMenuLuaRuntimeItemService itemService,
            GmMenuLuaRuntimeMobileService mobileService,
            GmMenuLuaRuntimeSpatialWorldService spatialWorldService,
            GmMenuLuaRuntimePlayerTargetService targetService,
            GameSession session,
            MoongateTCPClient client
        )
        {
            TempDirectory = tempDirectory;
            Service = service;
            Queue = queue;
            SessionService = sessionService;
            GumpDispatcher = gumpDispatcher;
            ItemTemplateService = itemTemplateService;
            ItemService = itemService;
            MobileService = mobileService;
            SpatialWorldService = spatialWorldService;
            TargetService = targetService;
            Session = session;
            Client = client;
        }

        public TempDirectory TempDirectory { get; }

        public LuaScriptEngineService Service { get; }

        public BasePacketListenerTestOutgoingPacketQueue Queue { get; }

        public FakeGameNetworkSessionService SessionService { get; }

        public GumpScriptDispatcherService GumpDispatcher { get; }

        public GmMenuLuaRuntimeItemTemplateService ItemTemplateService { get; }

        public GmMenuLuaRuntimeItemService ItemService { get; }

        public GmMenuLuaRuntimeMobileService MobileService { get; }

        public GmMenuLuaRuntimeSpatialWorldService SpatialWorldService { get; }

        public GmMenuLuaRuntimePlayerTargetService TargetService { get; }

        public GameSession Session { get; }

        public MoongateTCPClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            TempDirectory.Dispose();
        }
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_ShouldOpenCompressedMenuGumpWithAddDefaultTab()
    {
        using var context = await CreateRuntimeContextAsync();

        var result = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(context.Queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(context.Session.SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());
                var gump = (CompressedGumpPacket)outbound.Packet;
                Assert.That(gump.GumpId, Is.EqualTo(0xB930u));
                Assert.That(gump.Layout, Does.Contain("{ resizepic 0 0 5054"));
                Assert.That(gump.Layout, Does.Not.Contain("{ checkertrans"));
                Assert.That(gump.Layout, Does.Not.Contain("{ text 64 128 0 "));
                Assert.That(gump.Layout, Does.Not.Contain("{ text 478 114 0 "));
                Assert.That(gump.Layout, Does.Not.Contain("{ textentrylimited 248 112 190 20 0 "));
                Assert.That(gump.TextLines, Does.Contain("GM Menu"));
                Assert.That(gump.TextLines, Does.Contain("Add"));
                Assert.That(gump.TextLines, Does.Contain("Travel"));
                Assert.That(gump.TextLines, Does.Contain("Search Items and NPCs"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenSearchingManyItems_ShouldRenderCompactResultPage()
    {
        using var context = await CreateRuntimeContextAsync();
        context.ItemTemplateService.UpsertRange(
            Enumerable.Range(1, 8).Select(
                static index => new ItemTemplateDefinition
                {
                    Id = $"test_item_{index:00}",
                    Name = $"Test Item {index:00}",
                    ItemId = "0x0EED",
                    IsMovable = true,
                    Weight = 1.0m,
                    ScriptId = string.Empty
                }
            )
        );

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            102,
            new Dictionary<ushort, string>
            {
                [1] = "Test Item",
                [2] = "1"
            }
        );

        Assert.That(context.Queue.TryDequeue(out var searchOutbound), Is.True);
        Assert.That(searchOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var searchGump = (CompressedGumpPacket)searchOutbound.Packet;

        Assert.Multiple(
            () =>
            {
                Assert.That(searchGump.TextLines, Has.Some.Contains("Test Item 01"));
                Assert.That(searchGump.TextLines, Has.Some.Contains("Test Item 07"));
                Assert.That(searchGump.TextLines, Has.None.Contains("Test Item 08"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenSearchingItem_ShouldSelectPreviewAndAddToBackpack()
    {
        using var context = await CreateRuntimeContextAsync();

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            102,
            new Dictionary<ushort, string>
            {
                [1] = "gold",
                [2] = "25"
            }
        );

        Assert.That(context.Queue.TryDequeue(out var searchOutbound), Is.True);
        Assert.That(searchOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var searchGump = (CompressedGumpPacket)searchOutbound.Packet;
        Assert.That(searchGump.TextLines, Has.Some.Contains("Gold Coin"));

        DispatchButton(
            context,
            0xB930,
            200,
            new Dictionary<ushort, string>
            {
                [1] = "gold",
                [2] = "25"
            }
        );

        Assert.That(context.Queue.TryDequeue(out var selectionOutbound), Is.True);
        Assert.That(selectionOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var selectionGump = (CompressedGumpPacket)selectionOutbound.Packet;
        Assert.That(selectionGump.TextLines, Has.Some.Contains("Template: gold_coin"));
        Assert.That(selectionGump.TextLines, Has.Some.Contains("Item ID: 0x0EED"));

        DispatchButton(
            context,
            0xB930,
            301,
            new Dictionary<ushort, string>
            {
                [1] = "gold",
                [2] = "25"
            }
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(context.ItemService.LastSpawnTemplateId, Is.EqualTo("gold_coin"));
                Assert.That(context.ItemService.LastContainerId, Is.EqualTo(context.Session.Character!.BackpackId));
                Assert.That(context.ItemService.LastUpsertedItem, Is.Not.Null);
                Assert.That(context.ItemService.LastUpsertedItem!.Amount, Is.EqualTo(25));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenTargetingGroundForItem_ShouldSpawnAtSelectedLocation()
    {
        using var context = await CreateRuntimeContextAsync();

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            102,
            new Dictionary<ushort, string>
            {
                [1] = "gold",
                [2] = "10"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            200,
            new Dictionary<ushort, string>
            {
                [1] = "gold",
                [2] = "10"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            300,
            new Dictionary<ushort, string>
            {
                [1] = "gold",
                [2] = "10"
            }
        );

        Assert.That(context.TargetService.RequestCount, Is.EqualTo(1));
        context.TargetService.ResolveLocation(111, 222, 7);

        Assert.Multiple(
            () =>
            {
                Assert.That(context.ItemService.LastSpawnTemplateId, Is.EqualTo("gold_coin"));
                Assert.That(context.ItemService.LastWorldLocation, Is.EqualTo(new Point3D(111, 222, 7)));
                Assert.That(context.ItemService.LastWorldMapId, Is.EqualTo(1));
                Assert.That(context.ItemService.LastUpsertedItem, Is.Not.Null);
                Assert.That(context.ItemService.LastUpsertedItem!.Amount, Is.EqualTo(10));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenUsingNpcBrush_ShouldRespawnTargetCursorUntilStopped()
    {
        using var context = await CreateRuntimeContextAsync();

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            101,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            102,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );
        Assert.That(context.Queue.TryDequeue(out var searchOutbound), Is.True);
        Assert.That(searchOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var searchGump = (CompressedGumpPacket)searchOutbound.Packet;
        Assert.That(searchGump.TextLines, Has.Some.Contains("Zombie"));

        DispatchButton(
            context,
            0xB930,
            200,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            302,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );

        Assert.That(context.TargetService.RequestCount, Is.EqualTo(1));
        Assert.That(context.Queue.TryDequeue(out var brushOutbound), Is.True);
        Assert.That(brushOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var brushGump = (CompressedGumpPacket)brushOutbound.Packet;
        Assert.That(brushGump.TextLines, Has.Some.Contains("Brush Active: Zombie"));

        context.TargetService.ResolveLocation(333, 444, 0);

        Assert.Multiple(
            () =>
            {
                Assert.That(context.MobileService.SpawnCalls.Count, Is.EqualTo(2));
                Assert.That(context.MobileService.SpawnCalls[0], Is.EqualTo(("zombie", new Point3D(333, 444, 0), 1)));
                Assert.That(context.MobileService.SpawnCalls[1], Is.EqualTo(("zombie", new Point3D(333, 444, 0), 1)));
                Assert.That(context.TargetService.RequestCount, Is.EqualTo(2));
            }
        );

        DispatchButton(
            context,
            0xB930,
            303,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );

        Assert.That(context.TargetService.LastCancelCursorId, Is.Not.EqualTo(Serial.Zero));
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenBrushTargetIsCancelled_ShouldStopBrushWithoutReRequestingCursor()
    {
        using var context = await CreateRuntimeContextAsync();

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            101,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            102,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            200,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            302,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "2"
            }
        );

        Assert.That(context.TargetService.RequestCount, Is.EqualTo(1));
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        context.TargetService.ResolveCancel();

        Assert.Multiple(
            () =>
            {
                Assert.That(context.MobileService.SpawnCalls, Is.Empty);
                Assert.That(context.TargetService.RequestCount, Is.EqualTo(1));
                Assert.That(context.Queue.TryDequeue(out var cancelOutbound), Is.True);
                Assert.That(cancelOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
                var cancelGump = (CompressedGumpPacket)cancelOutbound.Packet;
                Assert.That(cancelGump.TextLines, Has.None.Contains("Brush Active: Zombie"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenTargetingGroundForNpc_ShouldAddSpawnedMobileToSpatialWorld()
    {
        using var context = await CreateRuntimeContextAsync();

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            101,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "1"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            102,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "1"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            200,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "1"
            }
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        DispatchButton(
            context,
            0xB930,
            300,
            new Dictionary<ushort, string>
            {
                [1] = "zombie",
                [2] = "1"
            }
        );

        Assert.That(context.TargetService.RequestCount, Is.EqualTo(1));
        context.TargetService.ResolveLocation(150, 250, 0);

        Assert.Multiple(
            () =>
            {
                Assert.That(context.MobileService.SpawnCalls.Count, Is.EqualTo(1));
                Assert.That(context.SpatialWorldService.AddedOrUpdatedMobiles.Count, Is.EqualTo(1));
                Assert.That(context.SpatialWorldService.AddedOrUpdatedMobiles[0].Name, Is.EqualTo("zombie"));
                Assert.That(context.SpatialWorldService.AddedOrUpdatedMobiles[0].Location, Is.EqualTo(new Point3D(150, 250, 0)));
                Assert.That(context.SpatialWorldService.AddedOrUpdatedMobiles[0].MapId, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenSwitchingToTravel_ShouldRenderTeleportBrowserAndTeleport()
    {
        using var context = await CreateRuntimeContextAsync();

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out _), Is.True);

        var travelPacket = new GumpMenuSelectionPacket();
        Assert.That(
            travelPacket.TryParse(BuildGumpResponsePacket((uint)context.Session.CharacterId, 0xB930, 11)),
            Is.True
        );
        Assert.That(context.GumpDispatcher.TryDispatch(context.Session, travelPacket), Is.True);
        Assert.That(context.Queue.TryDequeue(out var travelOutbound), Is.True);
        Assert.That(travelOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var travelGump = (CompressedGumpPacket)travelOutbound.Packet;
        Assert.That(travelGump.Layout, Does.Not.Contain("{ checkertrans"));
        Assert.That(travelGump.TextLines, Does.Contain("Step 1/3 - Select map"));
        Assert.That(travelGump.TextLines, Has.Some.Contains("Felucca"));

        DispatchButton(context, 0xB930, 101);
        Assert.That(context.Queue.TryDequeue(out _), Is.True);
        DispatchButton(context, 0xB930, 20);

        Assert.That(context.Queue.TryDequeue(out var categoryOutbound), Is.True);
        Assert.That(categoryOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var categoryGump = (CompressedGumpPacket)categoryOutbound.Packet;
        Assert.That(categoryGump.TextLines, Does.Contain("Step 2/3 - Select category"));
        Assert.That(categoryGump.TextLines, Has.Some.Contains("Towns"));

        DispatchButton(context, 0xB930, 201);
        Assert.That(context.Queue.TryDequeue(out _), Is.True);
        DispatchButton(context, 0xB930, 21);

        Assert.That(context.Queue.TryDequeue(out var locationOutbound), Is.True);
        Assert.That(locationOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var locationGump = (CompressedGumpPacket)locationOutbound.Packet;
        Assert.That(locationGump.TextLines, Does.Contain("Step 3/3 - Select location"));
        Assert.That(locationGump.TextLines, Has.Some.Contains("Britain Bank"));

        DispatchButton(context, 0xB930, 301);
        Assert.That(context.Queue.TryDequeue(out _), Is.True);
        DispatchButton(context, 0xB930, 15);

        Assert.Multiple(
            () =>
            {
                Assert.That(context.Session.Character, Is.Not.Null);
                Assert.That(context.Session.Character!.MapId, Is.EqualTo(0));
                Assert.That(context.Session.Character.Location, Is.EqualTo(new Point3D(1496, 1628, 20)));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGmMenuScripts_WhenSwitchingToProbe_ShouldRenderSpacingCalibrationSamples()
    {
        using var context = await CreateRuntimeContextAsync();

        _ = context.Service.ExecuteFunction(
            $"(function() return on_gm_menu_request({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out var initialOutbound), Is.True);
        Assert.That(initialOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var initialGump = (CompressedGumpPacket)initialOutbound.Packet;
        Assert.That(initialGump.TextLines, Does.Contain("Probe"));

        var probePacket = new GumpMenuSelectionPacket();
        Assert.That(
            probePacket.TryParse(BuildGumpResponsePacket((uint)context.Session.CharacterId, 0xB930, 12)),
            Is.True
        );
        Assert.That(context.GumpDispatcher.TryDispatch(context.Session, probePacket), Is.True);
        Assert.That(context.Queue.TryDequeue(out var probeOutbound), Is.True);
        Assert.That(probeOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var probeGump = (CompressedGumpPacket)probeOutbound.Packet;

        Assert.Multiple(
            () =>
            {
                Assert.That(probeGump.TextLines, Does.Contain("Spacing Probe"));
                Assert.That(probeGump.TextLines, Does.Contain("Compact 16/36"));
                Assert.That(probeGump.TextLines, Does.Contain("Balanced 16/38"));
                Assert.That(probeGump.TextLines, Does.Contain("Relaxed 18/40"));
                Assert.That(probeGump.TextLines, Has.Some.Contains("Extremely Long Decorative Plate Legs"));
                Assert.That(probeGump.TextLines, Has.Some.Contains("decorative_plate_legs_east"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithTeleportScripts_ShouldStillOpenStandaloneTeleportBrowser()
    {
        using var context = await CreateRuntimeContextAsync();

        var result = context.Service.ExecuteFunction(
            $"(function() local teleports = require(\"gumps.teleports\") return teleports.open({context.Session.SessionId}, {(uint)context.Session.CharacterId}) end)()"
        );

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo(true));
        Assert.That(context.Queue.TryDequeue(out var outbound), Is.True);
        Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());
        var gump = (CompressedGumpPacket)outbound.Packet;
        Assert.Multiple(
            () =>
            {
                Assert.That(gump.GumpId, Is.EqualTo(0xB61Fu));
                Assert.That(gump.Layout, Does.Contain("{ resizepic 0 0 5054 520 420 }"));
                Assert.That(gump.TextLines, Does.Contain("Teleport Browser"));
            }
        );
    }

    private static async Task<GmMenuRuntimeContext> CreateRuntimeContextAsync()
    {
        var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "interaction"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps", "gm_menu"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps", "gm_menu", "sections"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps", "teleports"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot =
            Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
        CopyScript(repoRoot, scriptsDir, "interaction/gm_menu.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/constants.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/state.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/ui.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/controller.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/render.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/sections/add.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/sections/probe.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/sections/travel.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports/constants.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports/controller.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports/render.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports/ui.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports/state.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports/actions.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/teleports/data.lua");

        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "init.lua"),
            "require(\"interaction.gm_menu\")\n"
        );

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var gumpDispatcher = new GumpScriptDispatcherService();
        var characterService = new GmMenuLuaRuntimeCharacterService();
        var speechService = new GmMenuLuaRuntimeSpeechService();
        var itemTemplateService = new GmMenuLuaRuntimeItemTemplateService();
        itemTemplateService.Upsert(
            new ItemTemplateDefinition
            {
                Id = "gold_coin",
                Name = "Gold Coin",
                ItemId = "0x0EED",
                IsMovable = true,
                Weight = 0.1m,
                ScriptId = string.Empty
            }
        );
        var mobileTemplateService = new GmMenuLuaRuntimeMobileTemplateService();
        mobileTemplateService.Upsert(
            new MobileTemplateDefinition
            {
                Id = "zombie",
                Name = "Zombie",
                Variants =
                [
                    new()
                    {
                        Appearance =
                        {
                            Body = 3
                        }
                    }
                ]
            }
        );
        var itemService = new GmMenuLuaRuntimeItemService();
        var mobileService = new GmMenuLuaRuntimeMobileService();
        var spatialWorldService = new GmMenuLuaRuntimeSpatialWorldService();
        var targetService = new GmMenuLuaRuntimePlayerTargetService();
        var locationCatalogService = new GmMenuLuaRuntimeLocationCatalogService
        {
            Locations =
            [
                new(0, "Felucca", "Towns", "Britain Bank", new(1496, 1628, 20))
            ]
        };
        var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000044u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00000044u,
                Name = "GM",
                MapId = 1,
                Location = new(100, 100, 0)
            }
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x00007000u,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = session.Character!.Location
        };
        session.Character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        session.Character.BackpackId = backpack.Id;
        characterService.Character = session.Character;
        itemService.Register(backpack);
        mobileService.Register(session.Character);
        sessionService.Add(session);

        var container = new Container();
        container.RegisterInstance<IOutgoingPacketQueue>(queue);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IGumpScriptDispatcherService>(gumpDispatcher);
        container.RegisterInstance<ICharacterService>(characterService);
        container.RegisterInstance<ISpeechService>(speechService);
        container.RegisterInstance<IItemTemplateService>(itemTemplateService);
        container.RegisterInstance<IMobileTemplateService>(mobileTemplateService);
        container.RegisterInstance<IItemService>(itemService);
        container.RegisterInstance<IMobileService>(mobileService);
        container.RegisterInstance<ISpatialWorldService>(spatialWorldService);
        container.RegisterInstance<IPlayerTargetService>(targetService);
        container.RegisterInstance<ILocationCatalogService>(locationCatalogService);

        var service = new LuaScriptEngineService(
            dirs,
            [
                new(typeof(GumpModule)),
                new(typeof(ItemModule)),
                new(typeof(MobileModule)),
                new(typeof(TargetModule)),
                new(typeof(LocationModule))
            ],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        return new(
            temp,
            service,
            queue,
            sessionService,
            gumpDispatcher,
            itemTemplateService,
            itemService,
            mobileService,
            spatialWorldService,
            targetService,
            session,
            client
        );
    }

    private static byte[] BuildGumpResponsePacket(
        uint serial,
        uint gumpId,
        uint buttonId,
        IReadOnlyDictionary<ushort, string>? textEntries = null
    )
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, true);

        bw.Write((byte)0xB1);
        bw.Write((ushort)0);
        WriteUInt32BE(bw, serial);
        WriteUInt32BE(bw, gumpId);
        WriteUInt32BE(bw, buttonId);
        WriteInt32BE(bw, 0);

        var entries = textEntries ?? new Dictionary<ushort, string>();
        WriteInt32BE(bw, entries.Count);

        foreach (var (id, text) in entries)
        {
            var value = text ?? string.Empty;
            var textBytes = Encoding.BigEndianUnicode.GetBytes(value);
            WriteUInt16BE(bw, id);
            WriteUInt16BE(bw, (ushort)value.Length);
            bw.Write(textBytes);
        }

        bw.Flush();
        var bytes = ms.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(1, 2), (ushort)bytes.Length);

        return bytes;
    }

    private static void CopyScript(string repoRoot, string scriptsDir, string relativePath)
    {
        var sourcePath = Path.Combine(repoRoot, "moongate_data", "scripts", relativePath);
        var destinationPath = Path.Combine(scriptsDir, relativePath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath);
    }

    private static void DispatchButton(
        GmMenuRuntimeContext context,
        uint gumpId,
        uint buttonId,
        IReadOnlyDictionary<ushort, string>? textEntries = null
    )
    {
        var packet = new GumpMenuSelectionPacket();
        Assert.That(
            packet.TryParse(BuildGumpResponsePacket((uint)context.Session.CharacterId, gumpId, buttonId, textEntries)),
            Is.True
        );
        Assert.That(context.GumpDispatcher.TryDispatch(context.Session, packet), Is.True);
    }

    private static void WriteInt32BE(BinaryWriter writer, int value)
        => writer.Write(BinaryPrimitives.ReverseEndianness(value));

    private static void WriteUInt16BE(BinaryWriter writer, ushort value)
        => writer.Write(BinaryPrimitives.ReverseEndianness(value));

    private static void WriteUInt32BE(BinaryWriter writer, uint value)
        => writer.Write(BinaryPrimitives.ReverseEndianness(value));
}
