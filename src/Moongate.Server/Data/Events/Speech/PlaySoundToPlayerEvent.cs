using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Speech;

/// <summary>
/// Event emitted to play a sound effect for a specific player character session.
/// </summary>
public readonly record struct PlaySoundToPlayerEvent(
    GameEventBase BaseEvent,
    Serial CharacterId,
    Point3D Location,
    ushort SoundModel,
    byte Mode,
    ushort Unknown3
) : IGameEvent
{
    /// <summary>
    /// Creates a player-targeted sound event with current timestamp.
    /// </summary>
    public PlaySoundToPlayerEvent(
        Serial characterId,
        Point3D location,
        ushort soundModel,
        byte mode = 0x01,
        ushort unknown3 = 0
    )
        : this(GameEventBase.CreateNow(), characterId, location, soundModel, mode, unknown3) { }
}
