namespace Moongate.Scripting.Internal;

/// <summary>Remembers which script file created each timer and coroutine, so invalidating a file can cancel exactly its work.</summary>
internal sealed class ScriptOwnership
{
    private readonly Dictionary<string, HashSet<string>> _timersByOwner = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Guid>> _coroutinesByOwner = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ownerByTimer = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, string> _ownerByCoroutine = new();

    public void TrackTimer(string owner, string timerId)
    {
        Add(_timersByOwner, owner, timerId);
        _ownerByTimer[timerId] = owner;
    }

    public void TrackCoroutine(string owner, Guid coroutineId)
    {
        Add(_coroutinesByOwner, owner, coroutineId);
        _ownerByCoroutine[coroutineId] = owner;
    }

    public void ForgetTimer(string timerId)
    {
        if (_ownerByTimer.Remove(timerId, out var owner) && _timersByOwner.TryGetValue(owner, out var set))
        {
            set.Remove(timerId);
        }
    }

    public void ForgetCoroutine(Guid coroutineId)
    {
        if (_ownerByCoroutine.Remove(coroutineId, out var owner) && _coroutinesByOwner.TryGetValue(owner, out var set))
        {
            set.Remove(coroutineId);
        }
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
