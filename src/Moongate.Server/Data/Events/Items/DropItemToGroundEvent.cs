using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Items;

/// <summary>
/// Event emitted when an item is dropped from a container/equipment to world ground.
/// </summary>
public readonly record struct DropItemToGroundEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial MobileId,
    Serial ItemId,
    Serial SourceContainerId,
    Point3D OldLocation,
    Point3D NewLocation
) : IGameEvent
{
    /// <summary>
    /// Creates an item-drop-to-ground event with current timestamp.
    /// </summary>
    public DropItemToGroundEvent(
        long sessionId,
        Serial mobileId,
        Serial itemId,
        Serial sourceContainerId,
        Point3D oldLocation,
        Point3D newLocation
    )
        : this(
            GameEventBase.CreateNow(),
            sessionId,
            mobileId,
            itemId,
            sourceContainerId,
            oldLocation,
            newLocation
        ) { }
}
