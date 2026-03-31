using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Scripting;

public sealed class PublicMoongateLuaRuntimeTests
{
    [Test]
    public async Task StartAsync_WithPublicMoongateScripts_ShouldOpenSharedPublicMoongateGump()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "items"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "moongates"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps", "moongates"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot = GetRepositoryRoot();
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "items", "public_moongate.lua"),
            Path.Combine(scriptsDir, "items", "public_moongate.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "moongates", "data.lua"),
            Path.Combine(scriptsDir, "moongates", "data.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "moongates", "public_moongate.lua"),
            Path.Combine(scriptsDir, "gumps", "moongates", "public_moongate.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "moongates", "constants.lua"),
            Path.Combine(scriptsDir, "gumps", "moongates", "constants.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "moongates", "state.lua"),
            Path.Combine(scriptsDir, "gumps", "moongates", "state.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "moongates", "ui.lua"),
            Path.Combine(scriptsDir, "gumps", "moongates", "ui.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "moongates", "render.lua"),
            Path.Combine(scriptsDir, "gumps", "moongates", "render.lua")
        );
        await File.WriteAllTextAsync(Path.Combine(scriptsDir, "init.lua"), "require(\"items.public_moongate\")\n");

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var gumpDispatcher = new GumpScriptDispatcherService();
        var mobileService = new PublicMoongateLuaRuntimeMobileService();
        var itemService = new PublicMoongateLuaRuntimeItemService();
        var characterService = new PublicMoongateLuaRuntimeCharacterService();

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005010u,
            Character = new()
            {
                Id = (Serial)0x00005010u,
                MapId = 0,
                Location = new Point3D(100, 100, 0),
                IsPlayer = true
            }
        };
        sessionService.Add(session);

        mobileService.MobilesById[session.Character!.Id] = session.Character!;

        itemService.ItemsById[(Serial)0x00006010u] = new()
        {
            Id = (Serial)0x00006010u,
            Name = "Public Moongate",
            MapId = 0,
            Location = new Point3D(100, 101, 0),
            ScriptId = "items.public_moongate",
            ItemId = 0x0F6C
        };

        var container = new Container();
        container.RegisterInstance<IOutgoingPacketQueue>(queue);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IGumpScriptDispatcherService>(gumpDispatcher);
        container.RegisterInstance<IMobileService>(mobileService);
        container.RegisterInstance<IItemService>(itemService);
        container.RegisterInstance<ICharacterService>(characterService);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule)), new(typeof(ItemModule)), new(typeof(MobileModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            "(function() return items_public_moongate.on_double_click({ session_id = 1, mobile_id = 0x00005010, item = { serial = 0x00006010 } }) end)()"
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());
                var gump = (CompressedGumpPacket)outbound.Packet;
                Assert.That(gump.TextLines, Contains.Item("Public Moongate"));
                Assert.That(gump.TextLines, Contains.Item("Britannia"));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));

    private sealed class PublicMoongateLuaRuntimeMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> MobilesById { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            MobilesById[mobile.Id] = mobile;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.FromResult(MobilesById.GetValueOrDefault(id));
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(int mapId, int sectorX, int sectorY, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(string templateId, Point3D location, int mapId, Serial? accountId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(string templateId, Point3D location, int mapId, Serial? accountId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class PublicMoongateLuaRuntimeItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> ItemsById { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(ItemsById.Remove(itemId));

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult<Moongate.Server.Data.Items.DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(ItemsById.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(true);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            if (!ItemsById.TryGetValue(itemId, out var item))
            {
                return Task.FromResult(false);
            }

            item.Location = location;
            item.MapId = mapId;

            return Task.FromResult(true);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity { Id = (Serial)1u, Amount = 1 });

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((ItemsById.TryGetValue(itemId, out var item), item));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            ItemsById[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                ItemsById[item.Id] = item;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class PublicMoongateLuaRuntimeCharacterService : ICharacterService
    {
        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }
}
