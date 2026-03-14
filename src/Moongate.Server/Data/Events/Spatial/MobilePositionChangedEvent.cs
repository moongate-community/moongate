using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Spatial;

/// <summary>
/// Event emitted when a mobile position changes.
/// </summary>
public readonly record struct MobilePositionChangedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial MobileId,
    int OldMapId,
    int MapId,
    Point3D OldLocation,
    Point3D NewLocation,
    bool IsTeleport
) : IGameEvent
{
    /// <summary>
    /// Creates an event with current timestamp.
    /// </summary>
    public MobilePositionChangedEvent(
        long sessionId,
        Serial mobileId,
        int oldMapId,
        int mapId,
        Point3D oldLocation,
        Point3D newLocation,
        bool isTeleport = false
    )
        : this(GameEventBase.CreateNow(), sessionId, mobileId, oldMapId, mapId, oldLocation, newLocation, isTeleport) { }
}
