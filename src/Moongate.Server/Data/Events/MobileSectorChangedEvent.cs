using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events;

/// <summary>
/// Event emitted when a mobile crosses sector boundaries.
/// </summary>
public readonly record struct MobileSectorChangedEvent(
    GameEventBase BaseEvent,
    Serial MobileId,
    int MapId,
    int OldSectorX,
    int OldSectorY,
    int NewSectorX,
    int NewSectorY
) : IGameEvent
{
    /// <summary>
    /// Creates a sector-change event with current timestamp.
    /// </summary>
    public MobileSectorChangedEvent(
        Serial mobileId,
        int mapId,
        int oldSectorX,
        int oldSectorY,
        int newSectorX,
        int newSectorY
    )
        : this(GameEventBase.CreateNow(), mobileId, mapId, oldSectorX, oldSectorY, newSectorX, newSectorY) { }
}
