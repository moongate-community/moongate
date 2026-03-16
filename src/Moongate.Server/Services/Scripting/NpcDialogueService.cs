using Moongate.Server.Data.Config;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;
using System.Collections.Concurrent;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Coordinates prompt loading, memory persistence, OpenAI generation, and NPC speech.
/// </summary>
public sealed class NpcDialogueService : INpcDialogueService
{
    private static readonly ILogger Logger = Log.ForContext<NpcDialogueService>();
    private static readonly TimeSpan DialogueRequestTimeout = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Serial, ListenerQueueState> _listenerQueues = [];
    private readonly MoongateConfig _config;
    private readonly INpcAiPromptService _promptService;
    private readonly INpcAiMemoryService _memoryService;
    private readonly INpcAiRuntimeStateService _runtimeStateService;
    private readonly IOpenAiNpcDialogueClient _openAiNpcDialogueClient;
    private readonly ISpeechService _speechService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IAsyncWorkSchedulerService _asyncWorkSchedulerService;
    private readonly IBackgroundJobService _backgroundJobService;

    public NpcDialogueService(
        MoongateConfig config,
        INpcAiPromptService promptService,
        INpcAiMemoryService memoryService,
        INpcAiRuntimeStateService runtimeStateService,
        IOpenAiNpcDialogueClient openAiNpcDialogueClient,
        ISpeechService speechService,
        ISpatialWorldService spatialWorldService,
        IAsyncWorkSchedulerService asyncWorkSchedulerService,
        IBackgroundJobService backgroundJobService
    )
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
        _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
        _runtimeStateService = runtimeStateService ?? throw new ArgumentNullException(nameof(runtimeStateService));
        _openAiNpcDialogueClient = openAiNpcDialogueClient ?? throw new ArgumentNullException(nameof(openAiNpcDialogueClient));
        _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
        _spatialWorldService = spatialWorldService ?? throw new ArgumentNullException(nameof(spatialWorldService));
        _asyncWorkSchedulerService =
            asyncWorkSchedulerService ?? throw new ArgumentNullException(nameof(asyncWorkSchedulerService));
        _backgroundJobService = backgroundJobService ?? throw new ArgumentNullException(nameof(backgroundJobService));
    }

    public bool QueueIdle(UOMobileEntity npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        if (!IsEnabled())
        {
            return false;
        }

        var nearbyPlayers = GetNearbyPlayerNames(npc);
        if (nearbyPlayers.Count == 0)
        {
            return false;
        }

        if (!TryLoadPrompt(npc.Id, out var prompt))
        {
            return false;
        }

        if (!_runtimeStateService.TryAcquireIdle(
                npc.Id,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Math.Max(0, _config.Llm.IdleCooldownMilliseconds)
            ))
        {
            return false;
        }

        return QueueRequest(
            npc,
            new NpcDialogueRequest
            {
                NpcId = npc.Id,
                NpcName = npc.Name ?? string.Empty,
                Prompt = prompt,
                Memory = TrimMemory(_memoryService.LoadOrCreate(npc.Id, npc.Name ?? string.Empty)),
                IsIdle = true,
                NearbyPlayerNames = nearbyPlayers
            }
        );
    }

    public bool QueueListener(UOMobileEntity npc, UOMobileEntity sender, string text)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(sender);

        if (!IsEnabled() || string.IsNullOrWhiteSpace(text) || npc.Id == sender.Id)
        {
            return false;
        }

        if (!TryLoadPrompt(npc.Id, out var prompt))
        {
            return false;
        }

        var state = _listenerQueues.GetOrAdd(npc.Id, static _ => new());
        var queuedRequest = new QueuedListenerRequest(npc, BuildListenerRequest(npc, sender, text, prompt));

        lock (state.SyncRoot)
        {
            state.Pending.Enqueue(queuedRequest);

            if (state.InFlight)
            {
                return true;
            }
        }

        TryDispatchNextListener(npc.Id, state);

        return true;
    }

    private List<string> GetNearbyPlayerNames(UOMobileEntity npc)
    {
        var names = _spatialWorldService.GetNearbyMobiles(
                                         npc.Location,
                                         Math.Max(1, _config.Llm.IdleNearbyPlayerRange),
                                         npc.MapId
                                     )
                                     .Where(mobile => mobile.IsPlayer && mobile.Id != npc.Id)
                                     .Select(mobile => mobile.Name?.Trim())
                                     .Where(name => !string.IsNullOrWhiteSpace(name))
                                     .Distinct(StringComparer.Ordinal)
                                     .Cast<string>()
                                     .ToList();

        return names;
    }

    private bool IsEnabled()
        => _config.Llm.IsEnabled;

    private string TrimMemory(string memory)
    {
        if (string.IsNullOrWhiteSpace(memory))
        {
            return string.Empty;
        }

        var normalized = memory.Trim();
        var maxCharacters = Math.Max(256, _config.Llm.MaxMemoryCharacters);
        if (normalized.Length <= maxCharacters)
        {
            return normalized;
        }

        return normalized[^maxCharacters..];
    }

    private bool TryLoadPrompt(Serial npcId, out string prompt)
    {
        prompt = string.Empty;

        if (!_runtimeStateService.TryGetPromptFile(npcId, out var promptFile) || string.IsNullOrWhiteSpace(promptFile))
        {
            Logger.Debug("Npc {NpcId} has no ai prompt binding.", npcId);

            return false;
        }

        return _promptService.TryLoad(promptFile, out prompt);
    }

    private bool QueueRequest(UOMobileEntity npc, NpcDialogueRequest request)
    {
        return _asyncWorkSchedulerService.TrySchedule(
            "npc-dialogue",
            npc.Id,
            cancellationToken => _openAiNpcDialogueClient.GenerateAsync(request, cancellationToken),
            response => ApplyResponse(npc, response),
            ex => Logger.Error(ex, "Npc dialogue background work failed for npc {NpcId}.", npc.Id),
            DialogueRequestTimeout
        );
    }

    private NpcDialogueRequest BuildListenerRequest(UOMobileEntity npc, UOMobileEntity sender, string text, string prompt)
        => new()
        {
            NpcId = npc.Id,
            NpcName = npc.Name ?? string.Empty,
            Prompt = prompt,
            Memory = TrimMemory(_memoryService.LoadOrCreate(npc.Id, npc.Name ?? string.Empty)),
            IsIdle = false,
            SenderName = sender.Name ?? string.Empty,
            HeardText = text.Trim(),
            NearbyPlayerNames = GetNearbyPlayerNames(npc)
        };

    private void TryDispatchNextListener(Serial npcId, ListenerQueueState state)
    {
        QueuedListenerRequest? queued = null;

        lock (state.SyncRoot)
        {
            if (state.InFlight || !state.Pending.TryDequeue(out queued))
            {
                return;
            }

            state.InFlight = true;
        }

        var scheduled = QueueRequest(
            queued!.Npc,
            queued.Request,
            () => OnListenerRequestFinished(npcId, state),
            () => OnListenerRequestFinished(npcId, state)
        );

        if (!scheduled)
        {
            lock (state.SyncRoot)
            {
                state.InFlight = false;
                state.Pending.Enqueue(queued);
            }
        }
    }

    private void OnListenerRequestFinished(Serial npcId, ListenerQueueState state)
    {
        lock (state.SyncRoot)
        {
            state.InFlight = false;
        }

        _backgroundJobService.PostToGameLoop(() => TryDispatchNextListener(npcId, state));
    }

    private bool QueueRequest(
        UOMobileEntity npc,
        NpcDialogueRequest request,
        Action? onSuccess = null,
        Action? onError = null
    )
    {
        return _asyncWorkSchedulerService.TrySchedule(
            "npc-dialogue",
            npc.Id,
            cancellationToken => _openAiNpcDialogueClient.GenerateAsync(request, cancellationToken),
            response =>
            {
                try
                {
                    ApplyResponse(npc, response);
                }
                finally
                {
                    onSuccess?.Invoke();
                }
            },
            ex =>
            {
                try
                {
                    Logger.Error(ex, "Npc dialogue background work failed for npc {NpcId}.", npc.Id);
                }
                finally
                {
                    onError?.Invoke();
                }
            },
            DialogueRequestTimeout
        );
    }

    private void ApplyResponse(UOMobileEntity npc, NpcDialogueResponse? response)
    {
        if (response is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(response.MemorySummary))
        {
            _memoryService.Save(npc.Id, response.MemorySummary.Trim());
        }

        if (!response.ShouldSpeak || string.IsNullOrWhiteSpace(response.SpeechText))
        {
            return;
        }

        _speechService.SpeakAsMobileAsync(
                         npc,
                         response.SpeechText.Trim(),
                         range: Math.Max(1, _config.Llm.SpeechRange)
                     )
                     .GetAwaiter()
                     .GetResult();
    }

    private sealed class ListenerQueueState
    {
        public object SyncRoot { get; } = new();
        public Queue<QueuedListenerRequest> Pending { get; } = new();
        public bool InFlight { get; set; }
    }

    private sealed record QueuedListenerRequest(
        UOMobileEntity Npc,
        NpcDialogueRequest Request
    );
}
