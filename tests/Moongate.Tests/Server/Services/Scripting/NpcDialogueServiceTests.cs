using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Services.Scripting;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class NpcDialogueServiceTests
{
    private sealed class NpcDialoguePromptServiceStub : INpcAiPromptService
    {
        public string PromptToReturn { get; } = "You are Lilly.";

        public bool TryLoad(string promptFile, out string prompt)
        {
            _ = promptFile;
            prompt = PromptToReturn;

            return true;
        }
    }

    private sealed class NpcDialogueMemoryServiceStub : INpcAiMemoryService
    {
        public string MemoryToReturn { get; } = "[Core Memory]\nLilly remembers nothing yet.";

        public string? LastSavedMemory { get; private set; }

        public string LoadOrCreate(Serial npcId, string npcName)
        {
            _ = npcId;
            _ = npcName;

            return MemoryToReturn;
        }

        public void Save(Serial npcId, string memory)
        {
            _ = npcId;
            LastSavedMemory = memory;
        }
    }

    private sealed class NpcDialogueOpenAiClientStub : IOpenAiNpcDialogueClient
    {
        public int CallCount { get; private set; }

        public NpcDialogueRequest? LastRequest { get; private set; }

        public NpcDialogueResponse? ResponseToReturn { get; set; }

        public Task<NpcDialogueResponse?> GenerateAsync(
            NpcDialogueRequest request,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            CallCount++;
            LastRequest = request;

            return Task.FromResult(ResponseToReturn);
        }
    }

    private sealed class NpcDialogueSpeechServiceStub : ISpeechService
    {
        public int SpeakCallCount { get; private set; }

        public UOMobileEntity? LastSpeaker { get; private set; }

        public string? LastText { get; private set; }

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
            _ = range;
            _ = messageType;
            _ = hue;
            _ = font;
            _ = language;
            _ = cancellationToken;
            SpeakCallCount++;
            LastSpeaker = speaker;
            LastText = text;

            return Task.FromResult(1);
        }
    }

    private sealed class NpcDialogueAsyncWorkSchedulerStub : IAsyncWorkSchedulerService
    {
        public int ScheduleCount { get; private set; }

        public string? LastQueueName { get; private set; }

        public object? LastKey { get; private set; }

        public bool NextTryScheduleResult { get; } = true;

        public Func<CancellationToken, Task<object?>>? PendingWork { get; private set; }

        public Action<object?>? PendingResultCallback { get; private set; }

        public Action<Exception>? PendingErrorCallback { get; private set; }

        public TimeSpan? LastTimeout { get; private set; }

        public TaskCompletionSource<object?>? DeferredResultSource { get; set; }

        public bool KeepKeyInFlightUntilResultCallbackReturns { get; set; }

        private bool _hasInFlightKey;

        public void CompletePendingWork(object? result)
        {
            Assert.That(PendingResultCallback, Is.Not.Null);

            try
            {
                PendingResultCallback!(result);
            }
            finally
            {
                _hasInFlightKey = false;
            }
        }

        public async Task<object?> ExecutePendingWorkAsync(CancellationToken cancellationToken = default)
        {
            Assert.That(PendingWork, Is.Not.Null);

            return await PendingWork!(cancellationToken);
        }

        public bool TrySchedule<TKey, TResult>(
            string queueName,
            TKey key,
            Func<CancellationToken, Task<TResult>> backgroundWork,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null,
            TimeSpan? timeout = null
        )
            where TKey : notnull
        {
            if (KeepKeyInFlightUntilResultCallbackReturns && _hasInFlightKey)
            {
                return false;
            }

            ScheduleCount++;
            LastQueueName = queueName;
            LastKey = key;
            LastTimeout = timeout;
            _hasInFlightKey = true;

            PendingWork = async cancellationToken =>
                          {
                              if (DeferredResultSource is not null)
                              {
                                  await DeferredResultSource.Task.WaitAsync(cancellationToken);
                              }

                              return await backgroundWork(cancellationToken);
                          };
            PendingResultCallback = result => onGameLoopResult((TResult)result!);
            PendingErrorCallback = onGameLoopError;

            return NextTryScheduleResult;
        }
    }

    private sealed class NpcDialogueBackgroundJobServiceStub : IBackgroundJobService
    {
        private readonly Queue<Action> _pendingGameLoop = new();

        public void EnqueueBackground(Action job)
            => throw new NotSupportedException();

        public void EnqueueBackground(Func<Task> job)
            => throw new NotSupportedException();

        public int ExecutePendingOnGameLoop(int maxActions = 100)
        {
            var executed = 0;

            while (executed < maxActions && _pendingGameLoop.Count > 0)
            {
                _pendingGameLoop.Dequeue()();
                executed++;
            }

            return executed;
        }

        public void PostToGameLoop(Action action)
            => _pendingGameLoop.Enqueue(action);

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void Start(int? workerCount = null)
            => throw new NotSupportedException();

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class NpcDialogueSpatialWorldServiceStub : ISpatialWorldService
    {
        public List<UOMobileEntity> NearbyMobiles { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

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
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [.. NearbyMobiles];
        }

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
    }

    [Test]
    public void QueueIdle_WhenNoPlayersAreNearby_ShouldSkipScheduling()
    {
        var config = CreateEnabledConfig();
        var promptService = new NpcDialoguePromptServiceStub();
        var memoryService = new NpcDialogueMemoryServiceStub();
        var runtimeState = new NpcAiRuntimeStateService();
        runtimeState.BindPromptFile((Serial)0x100u, "lilly.txt");
        var openAiClient = new NpcDialogueOpenAiClientStub
        {
            ResponseToReturn = new()
            {
                ShouldSpeak = true,
                SpeechText = "Lovely weather today."
            }
        };
        var speechService = new NpcDialogueSpeechServiceStub();
        var spatialWorldService = new NpcDialogueSpatialWorldServiceStub();
        var scheduler = new NpcDialogueAsyncWorkSchedulerStub();
        var backgroundJobs = new NpcDialogueBackgroundJobServiceStub();
        var service = new NpcDialogueService(
            config,
            promptService,
            memoryService,
            runtimeState,
            openAiClient,
            speechService,
            spatialWorldService,
            scheduler,
            backgroundJobs
        );
        var npc = CreateNpc((Serial)0x100u, "Lilly", false);

        var handled = service.QueueIdle(npc);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.False);
                Assert.That(scheduler.ScheduleCount, Is.EqualTo(0));
                Assert.That(openAiClient.CallCount, Is.EqualTo(0));
                Assert.That(speechService.SpeakCallCount, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task QueueListener_WhenAnotherRequestIsInFlight_ShouldQueueAndProcessNextSpeech()
    {
        var config = CreateEnabledConfig();
        var promptService = new NpcDialoguePromptServiceStub();
        var memoryService = new NpcDialogueMemoryServiceStub();
        var runtimeState = new NpcAiRuntimeStateService();
        runtimeState.BindPromptFile((Serial)0x100u, "lilly.txt");
        var openAiClient = new NpcDialogueOpenAiClientStub
        {
            ResponseToReturn = new()
            {
                ShouldSpeak = true,
                SpeechText = "Hello again."
            }
        };
        var speechService = new NpcDialogueSpeechServiceStub();
        var spatialWorldService = new NpcDialogueSpatialWorldServiceStub();
        var scheduler = new NpcDialogueAsyncWorkSchedulerStub();
        var backgroundJobs = new NpcDialogueBackgroundJobServiceStub();
        var service = new NpcDialogueService(
            config,
            promptService,
            memoryService,
            runtimeState,
            openAiClient,
            speechService,
            spatialWorldService,
            scheduler,
            backgroundJobs
        );
        var npc = CreateNpc((Serial)0x100u, "Lilly", false);
        var sender = CreateNpc((Serial)0x200u, "Marcus", true);

        var first = service.QueueListener(npc, sender, "hello");
        var second = service.QueueListener(npc, sender, "hello again");
        var firstResponse = await scheduler.ExecutePendingWorkAsync();
        scheduler.CompletePendingWork(firstResponse);
        backgroundJobs.ExecutePendingOnGameLoop();
        var secondResponse = await scheduler.ExecutePendingWorkAsync();
        scheduler.CompletePendingWork(secondResponse);

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.True);
                Assert.That(scheduler.ScheduleCount, Is.EqualTo(2));
                Assert.That(openAiClient.CallCount, Is.EqualTo(2));
                Assert.That(openAiClient.LastRequest, Is.Not.Null);
                Assert.That(openAiClient.LastRequest!.HeardText, Is.EqualTo("hello again"));
                Assert.That(speechService.SpeakCallCount, Is.EqualTo(2));
            }
        );
    }

    [Test]
    public void QueueListener_WhenBackgroundWorkIsDeferred_ShouldReturnImmediatelyWithoutWaitingForCompletion()
    {
        var config = CreateEnabledConfig();
        var promptService = new NpcDialoguePromptServiceStub();
        var memoryService = new NpcDialogueMemoryServiceStub();
        var runtimeState = new NpcAiRuntimeStateService();
        runtimeState.BindPromptFile((Serial)0x100u, "lilly.txt");
        var openAiClient = new NpcDialogueOpenAiClientStub
        {
            ResponseToReturn = new()
            {
                ShouldSpeak = true,
                SpeechText = "Hello later."
            }
        };
        var speechService = new NpcDialogueSpeechServiceStub();
        var spatialWorldService = new NpcDialogueSpatialWorldServiceStub();
        var scheduler = new NpcDialogueAsyncWorkSchedulerStub
        {
            DeferredResultSource = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var backgroundJobs = new NpcDialogueBackgroundJobServiceStub();
        var service = new NpcDialogueService(
            config,
            promptService,
            memoryService,
            runtimeState,
            openAiClient,
            speechService,
            spatialWorldService,
            scheduler,
            backgroundJobs
        );
        var npc = CreateNpc((Serial)0x100u, "Lilly", false);
        var sender = CreateNpc((Serial)0x200u, "Marcus", true);

        var startedAt = DateTime.UtcNow;
        var queued = service.QueueListener(npc, sender, "hello there");
        var elapsed = DateTime.UtcNow - startedAt;

        Assert.Multiple(
            () =>
            {
                Assert.That(queued, Is.True);
                Assert.That(elapsed, Is.LessThan(TimeSpan.FromMilliseconds(100)));
                Assert.That(openAiClient.CallCount, Is.Zero);
                Assert.That(speechService.SpeakCallCount, Is.Zero);
            }
        );
    }

    [Test]
    public async Task QueueListener_WhenOpenAiReturnsSpeech_ShouldScheduleThenSpeakAndSaveMemoryOnCompletion()
    {
        var config = CreateEnabledConfig();
        var promptService = new NpcDialoguePromptServiceStub();
        var memoryService = new NpcDialogueMemoryServiceStub();
        var runtimeState = new NpcAiRuntimeStateService();
        runtimeState.BindPromptFile((Serial)0x100u, "lilly.txt");
        var openAiClient = new NpcDialogueOpenAiClientStub
        {
            ResponseToReturn = new()
            {
                ShouldSpeak = true,
                SpeechText = "Hello, Marcus!",
                MemorySummary = "[Core Memory]\nLilly likes Marcus."
            }
        };
        var speechService = new NpcDialogueSpeechServiceStub();
        var spatialWorldService = new NpcDialogueSpatialWorldServiceStub();
        var scheduler = new NpcDialogueAsyncWorkSchedulerStub();
        var backgroundJobs = new NpcDialogueBackgroundJobServiceStub();
        var service = new NpcDialogueService(
            config,
            promptService,
            memoryService,
            runtimeState,
            openAiClient,
            speechService,
            spatialWorldService,
            scheduler,
            backgroundJobs
        );
        var npc = CreateNpc((Serial)0x100u, "Lilly", false);
        var sender = CreateNpc((Serial)0x200u, "Marcus", true);

        var handled = service.QueueListener(npc, sender, "hello there");
        var response = await scheduler.ExecutePendingWorkAsync();
        scheduler.CompletePendingWork(response);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(scheduler.ScheduleCount, Is.EqualTo(1));
                Assert.That(scheduler.LastQueueName, Is.EqualTo("npc-dialogue"));
                Assert.That(scheduler.LastKey, Is.EqualTo((object)(Serial)0x100u));
                Assert.That(openAiClient.CallCount, Is.EqualTo(1));
                Assert.That(openAiClient.LastRequest, Is.Not.Null);
                Assert.That(openAiClient.LastRequest!.NpcName, Is.EqualTo("Lilly"));
                Assert.That(openAiClient.LastRequest.SenderName, Is.EqualTo("Marcus"));
                Assert.That(openAiClient.LastRequest.HeardText, Is.EqualTo("hello there"));
                Assert.That(speechService.SpeakCallCount, Is.EqualTo(1));
                Assert.That(speechService.LastSpeaker, Is.SameAs(npc));
                Assert.That(speechService.LastText, Is.EqualTo("Hello, Marcus!"));
                Assert.That(memoryService.LastSavedMemory, Is.EqualTo("[Core Memory]\nLilly likes Marcus."));
            }
        );
    }

    [Test]
    public async Task QueueListener_WhenSchedulerReleasesKeyAfterResultCallback_ShouldStillProcessQueuedSpeech()
    {
        var config = CreateEnabledConfig();
        var promptService = new NpcDialoguePromptServiceStub();
        var memoryService = new NpcDialogueMemoryServiceStub();
        var runtimeState = new NpcAiRuntimeStateService();
        runtimeState.BindPromptFile((Serial)0x100u, "lilly.txt");
        var openAiClient = new NpcDialogueOpenAiClientStub
        {
            ResponseToReturn = new()
            {
                ShouldSpeak = true,
                SpeechText = "Hello again."
            }
        };
        var speechService = new NpcDialogueSpeechServiceStub();
        var spatialWorldService = new NpcDialogueSpatialWorldServiceStub();
        var scheduler = new NpcDialogueAsyncWorkSchedulerStub
        {
            KeepKeyInFlightUntilResultCallbackReturns = true
        };
        var backgroundJobs = new NpcDialogueBackgroundJobServiceStub();
        var service = new NpcDialogueService(
            config,
            promptService,
            memoryService,
            runtimeState,
            openAiClient,
            speechService,
            spatialWorldService,
            scheduler,
            backgroundJobs
        );
        var npc = CreateNpc((Serial)0x100u, "Lilly", false);
        var sender = CreateNpc((Serial)0x200u, "Marcus", true);

        var first = service.QueueListener(npc, sender, "hello");
        var second = service.QueueListener(npc, sender, "hello again");
        var firstResponse = await scheduler.ExecutePendingWorkAsync();
        scheduler.CompletePendingWork(firstResponse);
        backgroundJobs.ExecutePendingOnGameLoop();
        var secondResponse = await scheduler.ExecutePendingWorkAsync();
        scheduler.CompletePendingWork(secondResponse);

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.True);
                Assert.That(scheduler.ScheduleCount, Is.EqualTo(2));
                Assert.That(openAiClient.CallCount, Is.EqualTo(2));
                Assert.That(openAiClient.LastRequest, Is.Not.Null);
                Assert.That(openAiClient.LastRequest!.HeardText, Is.EqualTo("hello again"));
                Assert.That(speechService.SpeakCallCount, Is.EqualTo(2));
            }
        );
    }

    private static MoongateConfig CreateEnabledConfig()
        => new()
        {
            Llm = new()
            {
                IsEnabled = true,
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                ListenerCooldownMilliseconds = 60_000,
                IdleCooldownMilliseconds = 300_000,
                IdleNearbyPlayerRange = 12,
                SpeechRange = 12
            }
        };

    private static UOMobileEntity CreateNpc(Serial serial, string name, bool isPlayer)
        => new()
        {
            Id = serial,
            Name = name,
            IsPlayer = isPlayer,
            MapId = 1,
            Location = new(100, 100, 0)
        };
}
