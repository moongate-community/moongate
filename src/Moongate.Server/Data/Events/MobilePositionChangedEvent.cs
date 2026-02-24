using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events;

/// <summary>
/// Event emitted when a mobile position changes.
/// </summary>
public readonly record struct MobilePositionChangedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial MobileId,
    int MapId,
    Point3D OldLocation,
    Point3D NewLocation
) : IGameEvent
{
    /// <summary>
    /// Creates an event with current timestamp.
    /// </summary>
    public MobilePositionChangedEvent(
        long sessionId,
        Serial mobileId,
        int mapId,
        Point3D oldLocation,
        Point3D newLocation
    )
        : this(GameEventBase.CreateNow(), sessionId, mobileId, mapId, oldLocation, newLocation) { }
}
