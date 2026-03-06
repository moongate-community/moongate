using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Items;

/// <summary>
/// Event emitted when an item is deleted from persistence.
/// </summary>
public readonly record struct ItemDeletedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial ItemId,
    Serial OldContainerId,
    Point3D OldLocation,
    int MapId
) : IGameEvent
{
    /// <summary>
    /// Creates an item deleted event with current timestamp.
    /// </summary>
    public ItemDeletedEvent(
        long sessionId,
        Serial itemId,
        Serial oldContainerId,
        Point3D oldLocation,
        int mapId
    )
        : this(
            GameEventBase.CreateNow(),
            sessionId,
            itemId,
            oldContainerId,
            oldLocation,
            mapId
        ) { }
}
