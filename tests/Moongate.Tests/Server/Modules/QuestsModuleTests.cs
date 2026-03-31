using System.Net.Sockets;
using System.Linq;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.UI;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class QuestsModuleTests
{
    private sealed class RecordingScriptEngineService : IScriptEngineService
    {
        public string? LastFunctionName { get; private set; }

        public object[]? LastFunctionArgs { get; private set; }

        public string? LastExecuteFunctionCommand { get; private set; }

        public void AddCallback(string name, Action<object[]> callback)
            => _ = (name, callback);

        public void AddConstant(string name, object value)
            => _ = (name, value);

        public void AddInitScript(string script)
            => _ = script;

        public void AddManualModuleFunction(string moduleName, string functionName, Action<object[]> callback)
            => _ = (moduleName, functionName, callback);

        public void AddManualModuleFunction<TInput, TOutput>(string moduleName, string functionName, Func<TInput?, TOutput> callback)
            => _ = (moduleName, functionName, callback);

        public void AddScriptModule(Type type)
            => _ = type;

        public void CallFunction(string functionName, params object[] args)
        {
            LastFunctionName = functionName;
            LastFunctionArgs = args;
        }

        public void ClearScriptCache() { }

        public void InvalidateScript(string filePath)
            => _ = filePath;

        public void ExecuteCallback(string name, params object[] args)
            => _ = (name, args);

        public void ExecuteEngineReady() { }

        public ScriptResult ExecuteFunction(string command)
        {
            LastExecuteFunctionCommand = command;

            return new() { Success = true, Data = true };
        }

        public Task<ScriptResult> ExecuteFunctionAsync(string command)
            => Task.FromResult(ExecuteFunction(command));

        public void ExecuteFunctionFromBootstrap(string name) { }

        public void ExecuteScript(string script) { }

        public void ExecuteScriptFile(string scriptFile) { }

        public ScriptExecutionMetrics GetExecutionMetrics()
            => new();

        public void RegisterGlobal(string name, object value) { }

        public void RegisterGlobalFunction(string name, Delegate func) { }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public string ToScriptEngineFunctionName(string name)
            => name;

        public bool UnregisterGlobal(string name)
            => true;

#pragma warning disable CS0067
        public event EventHandler<ScriptErrorInfo>? OnScriptError;
#pragma warning restore CS0067
    }

    private sealed class RecordingQuestService : IQuestService
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

            if (!npc.TryGetCustomString(MobileCustomParamKeys.Template.TemplateId, out var templateId) ||
                !string.Equals(templateId, "quest_giver_npc", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<QuestTemplateDefinition>>([]);
            }

            return Task.FromResult(Available);
        }

        public Task<IReadOnlyList<QuestProgressEntity>> GetActiveForNpcAsync(
            UOMobileEntity player,
            UOMobileEntity npc,
            CancellationToken cancellationToken = default
        )
        {
            _ = (player, npc, cancellationToken);

            if (!npc.TryGetCustomString(MobileCustomParamKeys.Template.TemplateId, out var templateId) ||
                !string.Equals(templateId, "quest_turn_in_npc", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<QuestProgressEntity>>([]);
            }

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

    private sealed class RecordingMobileService : IMobileService
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

    private sealed class RecordingSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => [.. _sessions.Values];

        public GameSession GetOrCreate(Moongate.Network.Client.MoongateTCPClient client)
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

    private sealed class RecordingCharacterService : ICharacterService
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

    private sealed class RecordingItemService : IItemService
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
            => Task.FromResult(new UOItemEntity { Id = (Serial)1u });

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    [Test]
    public void Open_WhenSessionAndNpcAreValid_ShouldInvokeQuestDialogRequestedScriptFunction()
    {
        var module = CreateModule();
        var session = _sessionService.GetAll().Single();

        var result = module.Open(session.SessionId, (uint)session.CharacterId, 0x2001);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(_scriptEngine.LastFunctionName, Is.Null);
                Assert.That(
                    _scriptEngine.LastExecuteFunctionCommand,
                    Is.EqualTo($"on_quest_dialog_requested({session.SessionId}, {(uint)session.CharacterId}, 8193)")
                );
            }
        );
    }

    [Test]
    public void Open_WhenNpcIsOnDifferentMap_ShouldReturnFalse()
    {
        var module = CreateModule();
        var session = _sessionService.GetAll().Single();
        var npc = _mobileService.MobilesById[(Serial)0x2001u];
        npc.MapId = 1;

        var result = module.Open(session.SessionId, (uint)session.CharacterId, 0x2001);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.False);
                Assert.That(_scriptEngine.LastExecuteFunctionCommand, Is.Null);
            }
        );
    }

    [Test]
    public void GetAvailable_WhenNpcHasAvailableQuests_ShouldReturnQuestRows()
    {
        var module = CreateModule(
            available: [
                new QuestTemplateDefinition
                {
                    Id = "starter.rat_hunt",
                    Name = "Rat Hunt",
                    Description = "Kill sewer rats.",
                    Category = "starter"
                }
            ]
        );
        var session = _sessionService.GetAll().Single();

        var table = module.GetAvailable(session.SessionId, (uint)session.CharacterId, 0x2001);

        Assert.Multiple(
            () =>
            {
                Assert.That(table.Length, Is.EqualTo(1));
                var row = table.Get(1).Table!;
                Assert.That(row.Get("quest_id").String, Is.EqualTo("starter.rat_hunt"));
                Assert.That(row.Get("name").String, Is.EqualTo("Rat Hunt"));
                Assert.That(row.Get("description").String, Is.EqualTo("Kill sewer rats."));
            }
        );
    }

    [Test]
    public void GetAvailable_WhenNpcIsOnDifferentMap_ShouldReturnEmptyTable()
    {
        var module = CreateModule(
            available: [
                new QuestTemplateDefinition
                {
                    Id = "starter.rat_hunt",
                    Name = "Rat Hunt",
                    Description = "Kill sewer rats.",
                    Category = "starter"
                }
            ]
        );
        var session = _sessionService.GetAll().Single();
        _mobileService.MobilesById[(Serial)0x2001u].MapId = 1;

        var table = module.GetAvailable(session.SessionId, (uint)session.CharacterId, 0x2001);

        Assert.That(table.Length, Is.EqualTo(0));
    }

    [Test]
    public void GetActive_WhenNpcHasReadyQuests_ShouldReturnQuestRows()
    {
        var module = CreateModule(
            npcTemplateId: "quest_turn_in_npc",
            available: [
                new QuestTemplateDefinition
                {
                    Id = "starter.rat_hunt",
                    Name = "Rat Hunt",
                    Description = "Kill sewer rats.",
                    Category = "starter",
                    QuestGiverTemplateIds = ["quest_giver_npc"],
                    CompletionNpcTemplateIds = ["quest_turn_in_npc"]
                }
            ],
            active: [
                new QuestProgressEntity
                {
                    QuestId = "starter.rat_hunt",
                    Status = QuestProgressStatusType.ReadyToTurnIn
                }
            ]
        );
        var session = _sessionService.GetAll().Single();

        var table = module.GetActive(session.SessionId, (uint)session.CharacterId, 0x2001);

        Assert.Multiple(
            () =>
            {
                Assert.That(table.Length, Is.EqualTo(1));
                var row = table.Get(1).Table!;
                Assert.That(row.Get("quest_id").String, Is.EqualTo("starter.rat_hunt"));
                Assert.That(row.Get("is_ready_to_turn_in").Boolean, Is.True);
            }
        );
    }

    [Test]
    public void GetActive_WhenNpcIsOutOfRange_ShouldReturnEmptyTable()
    {
        var module = CreateModule(
            npcTemplateId: "quest_turn_in_npc",
            available: [
                new QuestTemplateDefinition
                {
                    Id = "starter.rat_hunt",
                    Name = "Rat Hunt",
                    Description = "Kill sewer rats.",
                    Category = "starter",
                    QuestGiverTemplateIds = ["quest_giver_npc"],
                    CompletionNpcTemplateIds = ["quest_turn_in_npc"]
                }
            ],
            active: [
                new QuestProgressEntity
                {
                    QuestId = "starter.rat_hunt",
                    Status = QuestProgressStatusType.ReadyToTurnIn
                }
            ]
        );
        var session = _sessionService.GetAll().Single();
        _mobileService.MobilesById[(Serial)0x2001u].Location = new Point3D(100, 100, 0);

        var table = module.GetActive(session.SessionId, (uint)session.CharacterId, 0x2001);

        Assert.That(table.Length, Is.EqualTo(0));
    }

    [Test]
    public async Task AcceptAndComplete_WhenQuestServiceResolvesNpc_ShouldForwardQuestActions()
    {
        var module = CreateModule(
            npcTemplateId: "quest_turn_in_npc",
            available: [
                new QuestTemplateDefinition
                {
                    Id = "starter.rat_hunt",
                    Name = "Rat Hunt",
                    Description = "Kill sewer rats.",
                    Category = "starter",
                    QuestGiverTemplateIds = ["quest_giver_npc"],
                    CompletionNpcTemplateIds = ["quest_turn_in_npc"]
                }
            ]
        );
        var session = _sessionService.GetAll().Single();

        var accepted = module.Accept(session.SessionId, (uint)session.CharacterId, 0x2001, "starter.rat_hunt");
        var completed = module.Complete(session.SessionId, (uint)session.CharacterId, 0x2001, "starter.rat_hunt");

        Assert.Multiple(
            () =>
            {
                Assert.That(accepted, Is.True);
                Assert.That(completed, Is.True);
                Assert.That(_questService.LastAcceptedQuestId, Is.EqualTo("starter.rat_hunt"));
                Assert.That(_questService.LastCompletedQuestId, Is.EqualTo("starter.rat_hunt"));
            }
        );
    }

    [Test]
    public void AcceptAndComplete_WhenNpcMovesOutOfRange_ShouldFailAndNotCallQuestService()
    {
        var module = CreateModule(
            npcTemplateId: "quest_turn_in_npc",
            available: [
                new QuestTemplateDefinition
                {
                    Id = "starter.rat_hunt",
                    Name = "Rat Hunt",
                    Description = "Kill sewer rats.",
                    Category = "starter",
                    QuestGiverTemplateIds = ["quest_giver_npc"],
                    CompletionNpcTemplateIds = ["quest_turn_in_npc"]
                }
            ]
        );
        var session = _sessionService.GetAll().Single();
        _mobileService.MobilesById[(Serial)0x2001u].Location = new Point3D(100, 100, 0);

        var accepted = module.Accept(session.SessionId, (uint)session.CharacterId, 0x2001, "starter.rat_hunt");
        var completed = module.Complete(session.SessionId, (uint)session.CharacterId, 0x2001, "starter.rat_hunt");

        Assert.Multiple(
            () =>
            {
                Assert.That(accepted, Is.False);
                Assert.That(completed, Is.False);
                Assert.That(_questService.LastAcceptedQuestId, Is.Null);
                Assert.That(_questService.LastCompletedQuestId, Is.Null);
            }
        );
    }

    private readonly RecordingScriptEngineService _scriptEngine = new();
    private readonly RecordingQuestService _questService = new();
    private readonly RecordingMobileService _mobileService = new();
    private readonly RecordingSessionService _sessionService = new();
    private readonly RecordingCharacterService _characterService = new();
    private readonly RecordingItemService _itemService = new();

    private QuestsModule CreateModule(
        string npcTemplateId = "quest_giver_npc",
        IReadOnlyList<QuestTemplateDefinition>? available = null,
        IReadOnlyList<QuestProgressEntity>? active = null
    )
    {
        _questService.Available = available ?? [];
        _questService.Active = active ?? [];

        var questTemplateService = new QuestTemplateService();
        foreach (var template in available ?? [])
        {
            questTemplateService.Upsert(template);
        }

        var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x1001u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x1001u,
                MapId = 0,
                Location = new Point3D(10, 10, 0),
                IsPlayer = true
            }
        };
        _sessionService.Clear();
        _sessionService.Add(session);

        _mobileService.MobilesById[(Serial)0x2001u] = new UOMobileEntity
        {
            Id = (Serial)0x2001u,
            MapId = 0,
            Location = new Point3D(11, 10, 0),
            IsPlayer = false
        };
        _mobileService.MobilesById[(Serial)0x2001u].SetCustomString(MobileCustomParamKeys.Template.TemplateId, npcTemplateId);

        return new(
            _questService,
            questTemplateService,
            _sessionService,
            _mobileService,
            _scriptEngine
        );
    }
}
