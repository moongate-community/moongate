namespace Moongate.Scripting.Internal;

/// <summary>
///     Remembers which script file created each timer and coroutine, so invalidating a file can cancel exactly its work.
/// </summary>
internal sealed class ScriptOwnership
{
    private readonly Dictionary<string, HashSet<string>> _timersByOwner = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Guid>> _coroutinesByOwner = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ownerByTimer = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, string> _ownerByCoroutine = new();

    /// <summary>
    ///     Empties every map: timers, coroutines and their owners.
    /// </summary>
    public void Clear()
    {
        _timersByOwner.Clear();
        _coroutinesByOwner.Clear();
        _ownerByTimer.Clear();
        _ownerByCoroutine.Clear();
    }

    public void ForgetCoroutine(Guid coroutineId)
    {
        if (_ownerByCoroutine.Remove(coroutineId, out var owner) && _coroutinesByOwner.TryGetValue(owner, out var set))
        {
            set.Remove(coroutineId);
        }
    }

    public void ForgetTimer(string timerId)
    {
        if (_ownerByTimer.Remove(timerId, out var owner) && _timersByOwner.TryGetValue(owner, out var set))
        {
            set.Remove(timerId);
        }
    }

    /// <summary>
    ///     Releases and returns every tracked timer id, regardless of owner, clearing both timer maps.
    /// </summary>
    public IReadOnlyList<string> ReleaseAllTimers()
    {
        var ids = _ownerByTimer.Keys.ToArray();
        _timersByOwner.Clear();
        _ownerByTimer.Clear();

        return ids;
    }

    public IReadOnlyList<Guid> ReleaseCoroutines(string owner)
    {
        if (!_coroutinesByOwner.Remove(owner, out var set))
        {
            return [];
        }

        foreach (var id in set)
        {
            _ownerByCoroutine.Remove(id);
        }

        return set.ToArray();
    }

    public IReadOnlyList<string> ReleaseTimers(string owner)
    {
        if (!_timersByOwner.Remove(owner, out var set))
        {
            return [];
        }

        foreach (var id in set)
        {
            _ownerByTimer.Remove(id);
        }

        return set.ToArray();
    }

    public void TrackCoroutine(string owner, Guid coroutineId)
    {
        Add(_coroutinesByOwner, owner, coroutineId);
        _ownerByCoroutine[coroutineId] = owner;
    }

    public void TrackTimer(string owner, string timerId)
    {
        Add(_timersByOwner, owner, timerId);
        _ownerByTimer[timerId] = owner;
    }

    private static void Add<T>(Dictionary<string, HashSet<T>> map, string owner, T item)
    {
        if (!map.TryGetValue(owner, out var set))
        {
            set = [];
            map[owner] = set;
        }

        set.Add(item);
    }
}
