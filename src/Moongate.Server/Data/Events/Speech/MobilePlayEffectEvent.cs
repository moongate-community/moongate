using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Speech;

public readonly record struct MobilePlayEffectEvent(
    GameEventBase BaseEvent,
    Serial MobileId,
    int MapId,
    Point3D Location,
    ushort ItemId,
    byte Speed,
    byte Duration,
    int Hue,
    int RenderMode,
    ushort Effect,
    ushort ExplodeEffect,
    ushort ExplodeSound,
    byte Layer,
    ushort Unknown3
) : IGameEvent
{
    public MobilePlayEffectEvent(
        Serial mobileId,
        int mapId,
        Point3D location,
        ushort itemId,
        byte speed = 10,
        byte duration = 10,
        int hue = 0,
        int renderMode = 0,
        ushort effect = 0,
        ushort explodeEffect = 0,
        ushort explodeSound = 0,
        byte layer = 0xFF,
        ushort unknown3 = 0
    )
        : this(
            GameEventBase.CreateNow(),
            mobileId,
            mapId,
            location,
            itemId,
            speed,
            duration,
            hue,
            renderMode,
            effect,
            explodeEffect,
            explodeSound,
            layer,
            unknown3
        ) { }
}
