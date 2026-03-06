using System.Collections.Concurrent;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Services.Items;

/// <summary>
/// In-memory per-session drag state store.
/// </summary>
public sealed class PlayerDragService : IPlayerDragService
{
    private readonly ConcurrentDictionary<long, PlayerDragState> _states = new();

    public void Clear(long sessionId)
        => _states.TryRemove(sessionId, out _);

    public void SetPending(
        long sessionId,
        Serial itemId,
        int amount,
        Serial sourceContainerId,
        Point3D sourceLocation
    )
        => _states[sessionId] = new(itemId, amount, sourceContainerId, sourceLocation);

    public bool TryConsume(long sessionId, Serial itemId, out PlayerDragState state)
    {
        if (_states.TryGetValue(sessionId, out state) && state.ItemId == itemId)
        {
            _states.TryRemove(sessionId, out _);

            return true;
        }

        state = default;

        return false;
    }

    public bool TryGet(long sessionId, out PlayerDragState state)
        => _states.TryGetValue(sessionId, out state);
}
