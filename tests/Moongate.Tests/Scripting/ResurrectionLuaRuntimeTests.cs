using System.Buffers.Binary;
using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.UI;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Interaction;
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

public sealed class ResurrectionLuaRuntimeTests
{
    [Test]
    public async Task StartAsync_WithResurrectionScripts_ShouldOpenCompressedResurrectionGump()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "interaction"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot = GetRepositoryRoot();
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "interaction", "resurrection.lua"),
            Path.Combine(scriptsDir, "interaction", "resurrection.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "resurrection.lua"),
            Path.Combine(scriptsDir, "gumps", "resurrection.lua")
        );
        await File.WriteAllTextAsync(Path.Combine(scriptsDir, "init.lua"), "require(\"interaction.init\")\n");
        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "interaction", "init.lua"),
            "require(\"interaction.resurrection\")\n"
        );

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var gumpDispatcher = new GumpScriptDispatcherService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005001u
        };
        sessionService.Add(session);

        var container = new Container();
        container.RegisterInstance<IOutgoingPacketQueue>(queue);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IGumpScriptDispatcherService>(gumpDispatcher);
        container.RegisterInstance<IResurrectionOfferService>(new ResurrectionLuaRuntimeOfferService());
        container.RegisterInstance<IItemService>(new ResurrectionLuaRuntimeItemService());

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule)), new(typeof(ResurrectionModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            $"(function() return on_resurrection_offer({session.SessionId}, {(uint)session.CharacterId}, \"healer\") end)()"
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());
                var gump = (CompressedGumpPacket)outbound.Packet;
                Assert.That(gump.GumpId, Is.EqualTo(0xB940u));
                Assert.That(gump.TextLines, Contains.Item("Resurrection"));
                Assert.That(gump.TextLines, Contains.Item("Do you wish to return to the living world?"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithResurrectionScripts_ShouldDispatchAcceptButtonThroughResurrectionModule()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "interaction"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot = GetRepositoryRoot();
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "interaction", "resurrection.lua"),
            Path.Combine(scriptsDir, "interaction", "resurrection.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "resurrection.lua"),
            Path.Combine(scriptsDir, "gumps", "resurrection.lua")
        );
        await File.WriteAllTextAsync(Path.Combine(scriptsDir, "init.lua"), "require(\"interaction.init\")\n");
        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "interaction", "init.lua"),
            "require(\"interaction.resurrection\")\n"
        );

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var gumpDispatcher = new GumpScriptDispatcherService();
        var offerService = new ResurrectionLuaRuntimeOfferService
        {
            AcceptResult = true
        };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005002u
        };
        sessionService.Add(session);

        var container = new Container();
        container.RegisterInstance<IOutgoingPacketQueue>(queue);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IGumpScriptDispatcherService>(gumpDispatcher);
        container.RegisterInstance<IResurrectionOfferService>(offerService);
        container.RegisterInstance<IItemService>(new ResurrectionLuaRuntimeItemService());

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule)), new(typeof(ResurrectionModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        _ = service.ExecuteFunction(
            $"(function() return on_resurrection_offer({session.SessionId}, {(uint)session.CharacterId}, \"ankh\") end)()"
        );
        Assert.That(queue.TryDequeue(out var outbound), Is.True);
        Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());

        var acceptPacket = new GumpMenuSelectionPacket();
        Assert.That(
            acceptPacket.TryParse(BuildGumpResponsePacket((uint)session.CharacterId, 0xB940, 1)),
            Is.True
        );
        Assert.That(gumpDispatcher.TryDispatch(session, acceptPacket), Is.True);

        Assert.Multiple(
            () =>
            {
                Assert.That(offerService.AcceptCalls, Is.EqualTo(1));
                Assert.That(offerService.LastAcceptedSessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    private static byte[] BuildGumpResponsePacket(uint characterId, uint gumpId, int buttonId)
    {
        var buffer = new byte[23];
        buffer[0] = 0xB1;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(1, 2), (ushort)buffer.Length);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(3, 4), characterId);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(7, 4), gumpId);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(11, 4), (uint)buttonId);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(15, 4), 0);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(19, 4), 0);

        return buffer;
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));

    private sealed class ResurrectionLuaRuntimeOfferService : IResurrectionOfferService
    {
        public bool AcceptResult { get; set; }

        public int AcceptCalls { get; private set; }

        public long LastAcceptedSessionId { get; private set; }

        public void Decline(long sessionId)
            => _ = sessionId;

        public Task<bool> TryAcceptAsync(long sessionId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            AcceptCalls++;
            LastAcceptedSessionId = sessionId;

            return Task.FromResult(AcceptResult);
        }

        public Task<bool> TryCreateOfferAsync(
            long sessionId,
            Serial characterId,
            Moongate.Server.Types.Interaction.ResurrectionOfferSourceType sourceType,
            CancellationToken cancellationToken = default
        )
        {
            _ = sessionId;
            _ = characterId;
            _ = sourceType;
            _ = cancellationToken;

            return Task.FromResult(true);
        }

        public Task<bool> TryCreateOfferAsync(
            long sessionId,
            Serial characterId,
            Moongate.Server.Types.Interaction.ResurrectionOfferSourceType sourceType,
            Serial sourceSerial,
            int mapId,
            Point3D sourceLocation,
            CancellationToken cancellationToken = default
        )
        {
            _ = sessionId;
            _ = characterId;
            _ = sourceType;
            _ = sourceSerial;
            _ = mapId;
            _ = sourceLocation;
            _ = cancellationToken;

            return Task.FromResult(true);
        }
    }

    private sealed class ResurrectionLuaRuntimeItemService : IItemService
    {
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

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }
}
