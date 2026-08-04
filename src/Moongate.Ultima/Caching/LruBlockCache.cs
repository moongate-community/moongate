namespace Moongate.Ultima.Caching;

/// <summary>
/// Bounded LRU cache for map blocks, replacing the unbounded jagged arrays that previously kept every
/// block a caller ever touched for the lifetime of the process. Keyed by the block's packed
/// coordinates — see <see cref="Key" />.
/// <para>
/// Unlike <see cref="LruBitmapCache" /> there is no disposal to arrange: a block is a plain array, so
/// a caller still holding one that has been evicted simply keeps it alive through the garbage
/// collector. Eviction here can never pull the rug from under a reader, which is what forced that
/// cache to leave <c>DisposeOnEvict</c> off by default.
/// </para>
/// <para>
/// Thread safety: every public member is guarded by a single lock. The arrays this replaces were read
/// with an unsynchronized read-modify-write (<c>_landTiles[x][y] ??= Read(x, y)</c>) from both the
/// game loop and the HTTP threads that serve map tiles, so two readers could race on the same block.
/// A miss costs a file read, which dwarfs the lock; a hit costs tens of nanoseconds.
/// </para>
/// </summary>
public sealed class LruBlockCache<TValue> where TValue : class
{
    private readonly Lock _lock = new();

    private readonly LinkedList<KeyValuePair<long, TValue>> _list = new();

    private readonly Dictionary<long, LinkedListNode<KeyValuePair<long, TValue>>> _map;

    private int _capacity;
    private int _evictedCount;

    public LruBlockCache(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be non-negative.");
        }

        _capacity = capacity;
        _map = new(Math.Min(capacity, 4096));
    }

    /// <summary>Maximum number of blocks held. Lowering it evicts down to the new cap immediately.</summary>
    public int Capacity
    {
        get
        {
            lock (_lock)
            {
                return _capacity;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _map.Count;
            }
        }
    }

    /// <summary>
    /// Total blocks evicted since construction. Diagnostic, and what a test asserts to prove the
    /// bounding actually happens rather than merely being configured.
    /// </summary>
    public int EvictedCount
    {
        get
        {
            lock (_lock)
            {
                return _evictedCount;
            }
        }
    }

    /// <summary>Packs block coordinates into one key. Block counts are well under 2^31 on every facet.</summary>
    public static long Key(int x, int y)
        => ((long)x << 32) | (uint)y;

    public void Clear()
    {
        lock (_lock)
        {
            _list.Clear();
            _map.Clear();
        }
    }

    /// <summary>
    /// The cached block, or the one <paramref name="read" /> produces — stored and returned. The read
    /// runs while the lock is held, so a block is never decoded twice concurrently; that is the whole
    /// point of routing misses through here rather than filling the cache from outside.
    /// </summary>
    public TValue GetOrAdd(long key, Func<TValue> read)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);

                return node.Value.Value;
            }

            var value = read();

            if (_capacity == 0)
            {
                return value;
            }

            _list.AddFirst(new LinkedListNode<KeyValuePair<long, TValue>>(new(key, value)));
            _map[key] = _list.First!;
            EvictWhileOverCapacityNoLock();

            return value;
        }
    }

    /// <summary>Inserts or replaces a block, as the patch and block-removal paths do.</summary>
    public void Set(long key, TValue value)
    {
        lock (_lock)
        {
            if (_capacity == 0)
            {
                return;
            }

            if (_map.TryGetValue(key, out var existing))
            {
                _list.Remove(existing);
            }

            _list.AddFirst(new LinkedListNode<KeyValuePair<long, TValue>>(new(key, value)));
            _map[key] = _list.First!;
            EvictWhileOverCapacityNoLock();
        }
    }

    public void SetCapacity(int newCapacity)
    {
        if (newCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newCapacity));
        }

        lock (_lock)
        {
            _capacity = newCapacity;
            EvictWhileOverCapacityNoLock();
        }
    }

    public bool TryGet(long key, out TValue? value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.Value;

                return true;
            }

            value = null;

            return false;
        }
    }

    private void EvictWhileOverCapacityNoLock()
    {
        while (_map.Count > _capacity)
        {
            var lru = _list.Last;

            if (lru == null)
            {
                break;
            }

            _list.RemoveLast();
            _map.Remove(lru.Value.Key);
            _evictedCount++;
        }
    }
}
