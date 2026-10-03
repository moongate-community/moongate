using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Orders the timers of the live items by their due time. Nothing is removed when a timer stops or an item goes:
///     the taker checks each entry against the item's prop.
/// </summary>
public sealed class ItemTimerQueue : IItemTimerQueue
{
    public const string PropPrefix = "timer.";

    private readonly TimeProvider _time;
    private readonly PriorityQueue<ItemTimerEntry, long> _queue = new();

    public ItemTimerQueue(TimeProvider time)
    {
        _time = time;
    }

    public void Schedule(Serial item, string name, long dueAt)
    {
        _queue.Enqueue(new(item, name, dueAt), dueAt);
    }

    public void Track(ItemEntity item)
    {
        if (item.Props is not { Count: > 0 } props)
        {
            return;
        }

        foreach (var (key, value) in props)
        {
            if (key.Length > PropPrefix.Length && key.StartsWith(PropPrefix, StringComparison.Ordinal) && value is long dueAt)
            {
                Schedule(item.Id, key[PropPrefix.Length..], dueAt);
            }
        }
    }

    public IReadOnlyList<ItemTimerEntry> TakeDue()
    {
        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var due = new List<ItemTimerEntry>();

        while (_queue.TryPeek(out var entry, out var at) && at <= now)
        {
            _queue.Dequeue();
            due.Add(entry);
        }

        return due;
    }
}
