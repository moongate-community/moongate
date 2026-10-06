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

    /// <summary>
    ///     The entries the queue may hold before it drops those of timers scheduled again since.
    /// </summary>
    public const int CompactAbove = 1024;

    private readonly TimeProvider _time;
    private readonly PriorityQueue<ItemTimerEntry, long> _queue = new();

    // The last due time scheduled for each timer: an entry with another time is of a timer scheduled again since.
    private readonly Dictionary<(Serial Item, string Name), long> _latest = [];

    /// <summary>
    ///     Gets how many entries wait in the queue, the outdated ones included.
    /// </summary>
    public int Count => _queue.Count;

    public ItemTimerQueue(TimeProvider time)
    {
        _time = time;
    }

    public void Schedule(Serial item, string name, long dueAt)
    {
        // Tracked twice at the same time, such as an item added again: one entry is enough.
        if (_latest.TryGetValue((item, name), out var queued) && queued == dueAt)
        {
            return;
        }

        _latest[(item, name)] = dueAt;
        _queue.Enqueue(new(item, name, dueAt), dueAt);

        // A timer started again and again leaves its old entries behind until their time: they are dropped here.
        if (_queue.Count > CompactAbove && _queue.Count > _latest.Count * 2)
        {
            Compact();
        }
    }

    public void Track(ItemEntity item)
    {
        if (item.Props is not { Count: > 0 } props)
        {
            return;
        }

        foreach (var (key, value) in props)
        {
            if (key.Length > PropPrefix.Length && key.StartsWith(PropPrefix, StringComparison.Ordinal) &&
                value is long dueAt)
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

            if (_latest.TryGetValue((entry.Item, entry.Name), out var latest) && latest == at)
            {
                _latest.Remove((entry.Item, entry.Name));
                due.Add(entry);
            }
        }

        return due;
    }

    private void Compact()
    {
        _queue.Clear();

        foreach (var ((item, name), dueAt) in _latest)
        {
            _queue.Enqueue(new(item, name, dueAt), dueAt);
        }
    }
}
