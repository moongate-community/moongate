using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Items;

/// <summary>
/// Event emitted when an item is moved between container and world contexts.
/// </summary>
public readonly record struct ItemMovedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial ItemId,
    Serial OldContainerId,
    Serial NewContainerId,
    Point3D OldLocation,
    Point3D NewLocation,
    int MapId
) : IGameEvent
{
    /// <summary>
    /// Creates an item moved event with current timestamp.
    /// </summary>
    public ItemMovedEvent(
        long sessionId,
        Serial itemId,
        Serial oldContainerId,
        Serial newContainerId,
        Point3D oldLocation,
        Point3D newLocation,
        int mapId
    )
        : this(
            GameEventBase.CreateNow(),
            sessionId,
            itemId,
            oldContainerId,
            newContainerId,
            oldLocation,
            newLocation,
            mapId
        ) { }
}
