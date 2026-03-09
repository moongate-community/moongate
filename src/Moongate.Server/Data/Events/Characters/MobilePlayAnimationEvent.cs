using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Raised when a mobile requests an animation broadcast.
/// </summary>
public readonly record struct MobilePlayAnimationEvent(
    GameEventBase BaseEvent,
    Serial MobileId,
    int MapId,
    Point3D Location,
    short Action,
    short FrameCount,
    short RepeatCount,
    bool Forward,
    bool Repeat,
    byte Delay
) : IGameEvent
{
    public MobilePlayAnimationEvent(
        Serial mobileId,
        int mapId,
        Point3D location,
        short action,
        short frameCount = 5,
        short repeatCount = 1,
        bool forward = true,
        bool repeat = false,
        byte delay = 0
    )
        : this(
            GameEventBase.CreateNow(),
            mobileId,
            mapId,
            location,
            action,
            frameCount,
            repeatCount,
            forward,
            repeat,
            delay
        ) { }
}
