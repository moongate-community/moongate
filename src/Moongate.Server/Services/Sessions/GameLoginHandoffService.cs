using System.Collections.Concurrent;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Version;

namespace Moongate.Server.Services.Sessions;

public sealed class GameLoginHandoffService : IGameLoginHandoffService
{
    private const long TimeToLiveMs = 5 * 60 * 1000;

    private readonly ConcurrentDictionary<uint, GameLoginHandoff> _handoffs = new();

    public void Store(uint sessionKey, ClientType clientType, ClientVersion? clientVersion)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        PruneExpired(now);

        _handoffs[sessionKey] = new GameLoginHandoff(sessionKey, clientType, clientVersion, now);
    }

    public bool TryConsume(uint sessionKey, out GameLoginHandoff handoff)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        PruneExpired(now);

        if (_handoffs.TryRemove(sessionKey, out handoff!))
        {
            if (now - handoff.CreatedAtUnixTimeMs <= TimeToLiveMs)
            {
                return true;
            }
        }

        handoff = null!;

        return false;
    }

    private void PruneExpired(long nowUnixTimeMs)
    {
        foreach (var pair in _handoffs)
        {
            if (nowUnixTimeMs - pair.Value.CreatedAtUnixTimeMs > TimeToLiveMs)
            {
                _ = _handoffs.TryRemove(pair.Key, out _);
            }
        }
    }
}
