using System.Buffers.Binary;
using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.UI;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Services.Events;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Support;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Scripting;

public sealed class QuestLuaRuntimeTests
{
    private sealed class QuestLuaRuntimeQuestService : IQuestService
    {
        public IReadOnlyList<QuestTemplateDefinition> Available { get; set; } = [];

        public IReadOnlyList<QuestProgressEntity> Active { get; set; } = [];

        public string? LastAcceptedQuestId { get; private set; }

        public string? LastCompletedQuestId { get; private set; }

        public Task<bool> AcceptAsync(UOMobileEntity player, UOMobileEntity npc, string questId, CancellationToken cancellationToken = default)
        {
            _ = (player, npc, cancellationToken);
            LastAcceptedQuestId = questId;

            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<QuestTemplateDefinition>> GetAvailableForNpcAsync(
            UOMobileEntity player,
            UOMobileEntity npc,
            CancellationToken cancellationToken = default
        )
        {
            _ = (player, npc, cancellationToken);

            return Task.FromResult(Available);
        }

        public Task<IReadOnlyList<QuestProgressEntity>> GetActiveForNpcAsync(
            UOMobileEntity player,
            UOMobileEntity npc,
            CancellationToken cancellationToken = default
        )
        {
            _ = (player, npc, cancellationToken);

            return Task.FromResult(Active);
        }

        public Task<IReadOnlyList<QuestProgressEntity>> GetJournalAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<QuestProgressEntity>>([]);

        public Task OnMobileKilledAsync(UOMobileEntity player, UOMobileEntity killedMobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReevaluateInventoryAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> TryCompleteAsync(UOMobileEntity player, UOMobileEntity npc, string questId, CancellationToken cancellationToken = default)
        {
            _ = (player, npc, cancellationToken);
            LastCompletedQuestId = questId;

            return Task.FromResult(true);
        }
    }

    private sealed class QuestLuaRuntimeMobileService : IMobileService
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

    private sealed class QuestLuaRuntimeSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => [.. _sessions.Values];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            foreach (var value in _sessions.Values)
            {
                if (value.CharacterId == characterId)
                {
                    session = value;

                    return true;
                }
            }

            session = null!;

            return false;
        }
    }

    private sealed class QuestLuaRuntimeCharacterService : ICharacterService
    {
        public UOItemEntity? Backpack { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult(Backpack);
        }

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class QuestLuaRuntimeItemService : IItemService
    {
        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(true);

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult<Moongate.Server.Data.Items.DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(true);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity { Id = (Serial)1u, Amount = 1 });

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    [Test]
    public async Task StartAsync_WithQuestScripts_ShouldOpenSharedQuestDialog()
    {
        using var context = await CreateContextAsync(
                         available: [
                             new QuestTemplateDefinition
                             {
                                 Id = "starter.rat_hunt",
                                 Name = "Rat Hunt",
                                 Description = "Kill three sewer rats.",
                                 Category = "starter",
                                 QuestGiverTemplateIds = ["quest_giver_npc"],
                                 CompletionNpcTemplateIds = ["quest_giver_npc"],
                                 MaxActivePerCharacter = 1
                             }
                         ]
                     );

        var result = context.Service.ExecuteFunction(
            $"(function() return on_quest_dialog_requested({context.Session.SessionId}, {(uint)context.Session.CharacterId}, {(uint)context.Npc.Id}) end)()"
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(context.Queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());
                var gump = (CompressedGumpPacket)outbound.Packet;
                Assert.That(gump.TextLines, Contains.Item("Quest Journal"));
                Assert.That(gump.TextLines, Contains.Item("Rat Hunt"));
                Assert.That(gump.TextLines, Contains.Item("Kill three sewer rats."));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithQuestDialogRequestedEvent_ShouldOpenSharedQuestDialogThroughHandler()
    {
        using var context = await CreateContextAsync(
                         available: [
                             new QuestTemplateDefinition
                             {
                                 Id = "starter.rat_hunt",
                                 Name = "Rat Hunt",
                                 Description = "Kill three sewer rats.",
                                 Category = "starter",
                                 QuestGiverTemplateIds = ["quest_giver_npc"],
                                 CompletionNpcTemplateIds = ["quest_giver_npc"],
                                 MaxActivePerCharacter = 1
                             }
                         ]
                     );

        var eventBus = new GameEventBusService();
        eventBus.RegisterListener(new QuestDialogRequestedHandler(context.SessionService, context.Service));

        await eventBus.PublishAsync(new QuestDialogRequestedEvent(context.Session.SessionId, context.Npc.Id));

        Assert.That(context.Queue.TryDequeue(out var outbound), Is.True);
        Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());

        var gump = (CompressedGumpPacket)outbound.Packet;
        Assert.That(gump.TextLines, Contains.Item("Quest Journal"));
        Assert.That(gump.TextLines, Contains.Item("Rat Hunt"));
    }

    [Test]
    public async Task StartAsync_WithQuestScripts_ShouldAcceptAQuestFromTheDialog()
    {
        using var context = await CreateContextAsync(
                         available: [
                             new QuestTemplateDefinition
                             {
                                 Id = "starter.rat_hunt",
                                 Name = "Rat Hunt",
                                 Description = "Kill three sewer rats.",
                                 Category = "starter",
                                 QuestGiverTemplateIds = ["quest_giver_npc"],
                                 CompletionNpcTemplateIds = ["quest_giver_npc"],
                                 MaxActivePerCharacter = 1
                             }
                         ]
                     );

        _ = context.Service.ExecuteFunction(
            $"(function() return on_quest_dialog_requested({context.Session.SessionId}, {(uint)context.Session.CharacterId}, {(uint)context.Npc.Id}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out var outbound), Is.True);
        Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());

        var acceptPacket = new GumpMenuSelectionPacket();
        Assert.That(acceptPacket.TryParse(BuildGumpResponsePacket((uint)context.Session.CharacterId, 0xB950, 1000)), Is.True);
        Assert.That(context.GumpDispatcher.TryDispatch(context.Session, acceptPacket), Is.True);

        Assert.Multiple(
            () =>
            {
                Assert.That(context.QuestService.LastAcceptedQuestId, Is.EqualTo("starter.rat_hunt"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithQuestScripts_ShouldCompleteAReadyQuest()
    {
        using var context = await CreateContextAsync(
                         npcTemplateId: "quest_turn_in_npc",
                         available: [
                             new QuestTemplateDefinition
                             {
                                 Id = "starter.apple_delivery",
                                 Name = "Apple Delivery",
                                 Description = "Bring apples to the farmer.",
                                 Category = "starter",
                                 QuestGiverTemplateIds = ["quest_giver_npc"],
                                 CompletionNpcTemplateIds = ["quest_turn_in_npc"],
                                 MaxActivePerCharacter = 1
                             }
                         ],
                         active: [
                             new QuestProgressEntity
                             {
                                 QuestId = "starter.apple_delivery",
                                 Status = QuestProgressStatusType.ReadyToTurnIn
                             }
                         ]
                     );

        _ = context.Service.ExecuteFunction(
            $"(function() return on_quest_dialog_requested({context.Session.SessionId}, {(uint)context.Session.CharacterId}, {(uint)context.Npc.Id}) end)()"
        );
        Assert.That(context.Queue.TryDequeue(out var outbound), Is.True);
        Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());

        var completePacket = new GumpMenuSelectionPacket();
        Assert.That(completePacket.TryParse(BuildGumpResponsePacket((uint)context.Session.CharacterId, 0xB950, 2000)), Is.True);
        Assert.That(context.GumpDispatcher.TryDispatch(context.Session, completePacket), Is.True);

        Assert.Multiple(
            () =>
            {
                Assert.That(context.QuestService.LastCompletedQuestId, Is.EqualTo("starter.apple_delivery"));
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

    private static async Task<QuestLuaRuntimeContext> CreateContextAsync(
        string npcTemplateId = "quest_giver_npc",
        IReadOnlyList<QuestTemplateDefinition>? available = null,
        IReadOnlyList<QuestProgressEntity>? active = null
    )
    {
        var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "interaction"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps", "quests"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot = GetRepositoryRoot();
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "interaction", "quests.lua"),
            Path.Combine(scriptsDir, "interaction", "quests.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "quests", "quest_dialog.lua"),
            Path.Combine(scriptsDir, "gumps", "quests", "quest_dialog.lua")
        );
        await File.WriteAllTextAsync(Path.Combine(scriptsDir, "init.lua"), "require(\"interaction.init\")\n");
        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "interaction", "init.lua"),
            "require(\"interaction.quests\")\n"
        );

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new QuestLuaRuntimeSessionService();
        var gumpDispatcher = new GumpScriptDispatcherService();
        var questService = new QuestLuaRuntimeQuestService
        {
            Available = available ?? [],
            Active = active ?? []
        };
        var questTemplateService = new QuestTemplateService();
        foreach (var template in available ?? [])
        {
            questTemplateService.Upsert(template);
        }
        var mobileService = new QuestLuaRuntimeMobileService();
        var characterService = new QuestLuaRuntimeCharacterService();
        var itemService = new QuestLuaRuntimeItemService();

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00005001u,
            Character = new()
            {
                Id = (Serial)0x00005001u,
                MapId = 0,
                Location = new Point3D(100, 100, 0),
                IsPlayer = true
            }
        };
        sessionService.Add(session);

        var npc = new UOMobileEntity
        {
            Id = (Serial)0x00006001u,
            Name = "Quest Giver",
            MapId = 0,
            Location = new Point3D(101, 100, 0),
            IsPlayer = false
        };
        npc.SetCustomString(MobileCustomParamKeys.Template.TemplateId, npcTemplateId);
        mobileService.MobilesById[npc.Id] = npc;

        var container = new Container();
        container.RegisterInstance<IOutgoingPacketQueue>(queue);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IGumpScriptDispatcherService>(gumpDispatcher);
        container.RegisterInstance<IQuestService>(questService);
        container.RegisterInstance<IQuestTemplateService>(questTemplateService);
        container.RegisterInstance<IMobileService>(mobileService);
        container.RegisterInstance<ICharacterService>(characterService);
        container.RegisterInstance<IItemService>(itemService);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule)), new(typeof(QuestsModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        container.RegisterInstance<IScriptEngineService>(service);
        await service.StartAsync();

        return new(
            temp,
            queue,
            sessionService,
            gumpDispatcher,
            questService,
            mobileService,
            session,
            npc,
            service
        );
    }

    private sealed record QuestLuaRuntimeContext(
        TempDirectory TempDirectory,
        BasePacketListenerTestOutgoingPacketQueue Queue,
        QuestLuaRuntimeSessionService SessionService,
        GumpScriptDispatcherService GumpDispatcher,
        QuestLuaRuntimeQuestService QuestService,
        QuestLuaRuntimeMobileService MobileService,
        GameSession Session,
        UOMobileEntity Npc,
        LuaScriptEngineService Service
    ) : IDisposable
    {
        public void Dispose()
        {
            Service.Dispose();
            TempDirectory.Dispose();
        }
    }
}
