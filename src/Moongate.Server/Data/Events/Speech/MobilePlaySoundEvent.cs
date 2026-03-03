using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Speech;

/// <summary>
/// Event emitted when a mobile requests a world sound effect broadcast.
/// </summary>
public readonly record struct MobilePlaySoundEvent(
    GameEventBase BaseEvent,
    Serial MobileId,
    int MapId,
    Point3D Location,
    ushort SoundModel,
    byte Mode,
    ushort Unknown3
) : IGameEvent
{
    /// <summary>
    /// Creates a sound event with current timestamp.
    /// </summary>
    public MobilePlaySoundEvent(
        Serial mobileId,
        int mapId,
        Point3D location,
        ushort soundModel,
        byte mode = 0x01,
        ushort unknown3 = 0
    )
        : this(GameEventBase.CreateNow(), mobileId, mapId, location, soundModel, mode, unknown3) { }
}
