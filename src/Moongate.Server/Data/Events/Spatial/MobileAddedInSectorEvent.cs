using Moongate.UO.Data.Ids;
using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Spatial;

/// <summary>
/// Event emitted when a mobile is first indexed into a sector.
/// </summary>
public readonly record struct MobileAddedInSectorEvent(
    GameEventBase BaseEvent,
    Serial MobileId,
    int MapId,
    int SectorX,
    int SectorY
) : IGameEvent
{
    /// <summary>
    /// Creates a sector-add event with current timestamp.
    /// </summary>
    public MobileAddedInSectorEvent(Serial mobileId, int mapId, int sectorX, int sectorY)
        : this(GameEventBase.CreateNow(), mobileId, mapId, sectorX, sectorY) { }
}
