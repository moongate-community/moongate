using System.Collections.Concurrent;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;

namespace Moongate.Server.Services.Accounts;

/// <summary>In-memory auth-key store with a TTL sweep. Keys are single-use.</summary>
public sealed class PendingLoginStore : IPendingLoginStore
{
    private readonly long _ttlMilliseconds;
    private readonly Func<long> _nowMilliseconds;
    private readonly ConcurrentDictionary<uint, Entry> _entries = new();

    private int _counter;

    public PendingLoginStore(long ttlMilliseconds, Func<long> nowMilliseconds)
    {
        _ttlMilliseconds = ttlMilliseconds;
        _nowMilliseconds = nowMilliseconds;
    }

    private readonly record struct Entry(PendingLogin Login, long ExpiresAt);

    public uint Create(PendingLogin login)
    {
        var key = NextKey();
        _entries[key] = new(login, _nowMilliseconds() + _ttlMilliseconds);

        return key;
    }

    public bool TryTake(uint authKey, out PendingLogin login)
    {
        login = default;

        if (!_entries.TryRemove(authKey, out var entry))
        {
            return false;
        }

        if (_nowMilliseconds() > entry.ExpiresAt)
        {
            return false;
        }

        login = entry.Login;

        return true;
    }

    private uint NextKey()
    {
        var value = unchecked((uint)Interlocked.Increment(ref _counter));

        return value == 0 ? unchecked((uint)Interlocked.Increment(ref _counter)) : value;
    }
}
