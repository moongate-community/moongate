using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Data.Internal.AI;

public sealed class NpcBrainMailbox
{
    private readonly int _capacity;
    private readonly INpcAiMetrics _metrics;
    private readonly List<(NpcBrainHookType Hook, NpcBrainEvent Event, long Sequence)> _entries = [];

    private long _nextSequence;

    public bool HasEvents => _entries.Count > 0;

    public NpcBrainMailbox(int capacity, INpcAiMetrics metrics)
    {
        _capacity = capacity;
        _metrics = metrics;
    }

    public void Enqueue(NpcBrainHookType hook, NpcBrainEvent brainEvent)
    {
        if (TryCoalesce(hook, brainEvent))
        {
            _metrics.RecordEvent(NpcBrainEventDispositionType.Coalesced);
            return;
        }

        if (_entries.Count == _capacity && !MakeRoom(hook))
        {
            _metrics.RecordEvent(NpcBrainEventDispositionType.Dropped);
            return;
        }

        _entries.Add((hook, brainEvent, _nextSequence++));
    }

    public IReadOnlyList<(NpcBrainHookType Hook, NpcBrainEvent Event)> Dequeue(int maximumCount)
    {
        if (maximumCount <= 0 || _entries.Count == 0)
        {
            return [];
        }

        var count = Math.Min(maximumCount, _entries.Count);
        var delivered = new List<(NpcBrainHookType Hook, NpcBrainEvent Event)>(count);

        for (var index = 0; index < count; index++)
        {
            var entry = _entries[index];
            delivered.Add((entry.Hook, entry.Event));
            _metrics.RecordEvent(NpcBrainEventDispositionType.Delivered);
        }

        _entries.RemoveRange(0, count);

        return delivered;
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private bool TryCoalesce(NpcBrainHookType hook, NpcBrainEvent brainEvent)
    {
        if (brainEvent.Mobile is not { Id: var mobileId } || mobileId == Serial.Zero)
        {
            return false;
        }

        if (hook == NpcBrainHookType.MobileMoved)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];

                if (entry.Hook != hook || entry.Event.Mobile?.Id != mobileId)
                {
                    continue;
                }

                _entries[index] = (hook, brainEvent, entry.Sequence);
                return true;
            }
        }

        if (hook is NpcBrainHookType.MobileEnteredRange or NpcBrainHookType.MobileLeftRange)
        {
            for (var index = _entries.Count - 1; index >= 0; index--)
            {
                var entry = _entries[index];

                if (
                    entry.Event.Mobile?.Id != mobileId ||
                    entry.Hook is not (
                        NpcBrainHookType.MobileEnteredRange or NpcBrainHookType.MobileLeftRange
                    )
                )
                {
                    continue;
                }

                return entry.Hook == hook;
            }
        }

        return false;
    }

    private bool MakeRoom(NpcBrainHookType incomingHook)
    {
        var lowestPriority = _entries.Min(entry => Priority(entry.Hook));

        if (Priority(incomingHook) < lowestPriority)
        {
            return false;
        }

        var evictionIndex = _entries.FindIndex(entry => Priority(entry.Hook) == lowestPriority);
        _entries.RemoveAt(evictionIndex);
        _metrics.RecordEvent(NpcBrainEventDispositionType.Dropped);

        return true;
    }

    private static int Priority(NpcBrainHookType hook)
        => hook switch
        {
            NpcBrainHookType.Activate or NpcBrainHookType.Deactivate or NpcBrainHookType.Death => 4,
            NpcBrainHookType.Attacked or NpcBrainHookType.Damage => 3,
            NpcBrainHookType.SpeechHeard => 2,
            NpcBrainHookType.MobileEnteredRange or NpcBrainHookType.MobileLeftRange => 1,
            _ => 0
        };
}
