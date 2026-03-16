using System.Collections.Concurrent;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Stores runtime-only AI prompt bindings and cooldown timestamps.
/// </summary>
public sealed class NpcAiRuntimeStateService : INpcAiRuntimeStateService
{
    private readonly ConcurrentDictionary<Serial, RuntimeState> _states = [];

    public void BindPromptFile(Serial npcId, string promptFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptFile);
        var state = _states.GetOrAdd(npcId, static _ => new());

        lock (state.SyncRoot)
        {
            state.PromptFile = promptFile.Trim();
        }
    }

    public bool TryAcquireIdle(Serial npcId, long nowMilliseconds, int cooldownMilliseconds)
        => TryAcquire(npcId, nowMilliseconds, cooldownMilliseconds, isIdle: true);

    public bool TryAcquireListener(Serial npcId, long nowMilliseconds, int cooldownMilliseconds)
        => TryAcquire(npcId, nowMilliseconds, cooldownMilliseconds, isIdle: false);

    public bool TryGetPromptFile(Serial npcId, out string? promptFile)
    {
        promptFile = null;

        if (!_states.TryGetValue(npcId, out var state))
        {
            return false;
        }

        lock (state.SyncRoot)
        {
            promptFile = state.PromptFile;
        }

        return !string.IsNullOrWhiteSpace(promptFile);
    }

    private bool TryAcquire(Serial npcId, long nowMilliseconds, int cooldownMilliseconds, bool isIdle)
    {
        var state = _states.GetOrAdd(npcId, static _ => new());

        lock (state.SyncRoot)
        {
            var lastRequest = isIdle ? state.LastIdleRequestMilliseconds : state.LastListenerRequestMilliseconds;
            if (cooldownMilliseconds > 0 && lastRequest > 0 && nowMilliseconds - lastRequest < cooldownMilliseconds)
            {
                return false;
            }

            if (isIdle)
            {
                state.LastIdleRequestMilliseconds = nowMilliseconds;
            }
            else
            {
                state.LastListenerRequestMilliseconds = nowMilliseconds;
            }

            return true;
        }
    }

    private sealed class RuntimeState
    {
        public object SyncRoot { get; } = new();

        public string? PromptFile { get; set; }

        public long LastListenerRequestMilliseconds { get; set; }

        public long LastIdleRequestMilliseconds { get; set; }
    }
}
