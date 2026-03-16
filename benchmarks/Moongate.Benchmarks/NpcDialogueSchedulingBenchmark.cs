using BenchmarkDotNet.Attributes;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Services.EventLoop;
using Moongate.Server.Services.Scripting;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Benchmarks;

[MemoryDiagnoser, WarmupCount(3), IterationCount(12)]
public class NpcDialogueSchedulingBenchmark : IDisposable
{
    private BackgroundJobService _backgroundJobs = null!;
    private AsyncWorkSchedulerService _scheduler = null!;
    private BenchmarkOpenAiClient _openAiClient = null!;
    private BenchmarkSpeechService _speechService = null!;
    private NpcDialogueService _dialogueService = null!;
    private NpcAiRuntimeStateService _runtimeState = null!;
    private UOMobileEntity _npc = null!;
    private UOMobileEntity _sender = null!;

    private sealed class BenchmarkOpenAiClient : IOpenAiNpcDialogueClient
    {
        public Task<NpcDialogueResponse?> GenerateAsync(
            NpcDialogueRequest request,
            CancellationToken cancellationToken = default
        )
        {
            _ = request;
            _ = cancellationToken;

            return Task.FromResult<NpcDialogueResponse?>(
                new()
                {
                    ShouldSpeak = true,
                    SpeechText = "Hello, Marcus!",
                    MemorySummary = "[Core Memory]\nLilly greeted Marcus."
                }
            );
        }

        public void Reset() { }
    }

    private sealed class BenchmarkPromptService : INpcAiPromptService
    {
        public bool TryLoad(string promptFile, out string prompt)
        {
            _ = promptFile;
            prompt = "You are Lilly.";

            return true;
        }
    }

    private sealed class BenchmarkMemoryService : INpcAiMemoryService
    {
        public string LoadOrCreate(Serial npcId, string npcName)
        {
            _ = npcId;
            _ = npcName;

            return "[Core Memory]\nLilly knows the castle.";
        }

        public void Save(Serial npcId, string memory)
        {
            _ = npcId;
            _ = memory;
        }
    }

    private sealed class BenchmarkSpeechService : ISpeechService
    {
        public int SpokenCount { get; private set; }

        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
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

        public void Reset()
            => SpokenCount = 0;

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
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
            SpokenCount++;

            return Task.FromResult(1);
        }
    }

    private sealed class BenchmarkSpatialWorldService : ISpatialWorldService
    {
        public void AddOrUpdateItem(UOItemEntity item, int mapId)
            => _ = (item, mapId);

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => _ = mobile;

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
            _ = (packet, mapId, location, range, excludeSessionId);

            return Task.FromResult(0);
        }

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
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

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
            => _ = (item, mapId, oldLocation, newLocation);

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
            => _ = (mobile, oldLocation, newLocation);

        public void RemoveEntity(Serial serial)
            => _ = serial;
    }

    public void Dispose()
    {
        _backgroundJobs.Dispose();
        GC.SuppressFinalize(this);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await _backgroundJobs.StopAsync();
        _backgroundJobs.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _backgroundJobs = new();
        _backgroundJobs.Start(1);
        _scheduler = new(_backgroundJobs);
        _openAiClient = new();
        _speechService = new();
        _runtimeState = new();
        _runtimeState.BindPromptFile((Serial)0x100u, "lilly.txt");

        _dialogueService = new(
            new()
            {
                Llm = new()
                {
                    IsEnabled = true,
                    ApiKey = "benchmark-key",
                    Model = "gpt-5-mini",
                    ListenerCooldownMilliseconds = 0,
                    IdleCooldownMilliseconds = 0,
                    IdleNearbyPlayerRange = 12,
                    SpeechRange = 12
                }
            },
            new BenchmarkPromptService(),
            new BenchmarkMemoryService(),
            _runtimeState,
            _openAiClient,
            _speechService,
            new BenchmarkSpatialWorldService(),
            _scheduler,
            _backgroundJobs
        );

        _npc = new()
        {
            Id = (Serial)0x100u,
            Name = "Lilly",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        _sender = new()
        {
            Id = (Serial)0x200u,
            Name = "Marcus",
            IsPlayer = true,
            MapId = 1,
            Location = new(101, 100, 0)
        };
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _openAiClient.Reset();
        _speechService.Reset();
    }

    [Benchmark]
    public bool QueueListener_EnqueueOnly()
        => _dialogueService.QueueListener(_npc, _sender, "hello there");

    [Benchmark]
    public bool RejectDuplicate_InFlight()
    {
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = _scheduler.TrySchedule(
            "npc-dialogue",
            _npc.Id,
            _ => gate.Task,
            _ => { }
        );
        var second = _scheduler.TrySchedule(
            "npc-dialogue",
            _npc.Id,
            _ => Task.FromResult(2),
            _ => { }
        );

        gate.SetResult(1);
        SpinWait.SpinUntil(() => _backgroundJobs.ExecutePendingOnGameLoop() > 0, 1000);

        return first && !second;
    }

    [Benchmark]
    public async Task<int> ScheduleAndComplete_SingleNpc()
    {
        _scheduler.TrySchedule(
            "npc-dialogue",
            _npc.Id,
            _ => Task.FromResult(42),
            _ => { },
            null,
            TimeSpan.FromSeconds(5)
        );

        while (_backgroundJobs.ExecutePendingOnGameLoop() == 0)
        {
            await Task.Delay(1);
        }

        return 1;
    }
}
