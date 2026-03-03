using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Spatial;

/// <summary>
/// Event emitted when an item is indexed into a sector.
/// </summary>
public readonly record struct ItemAddedInSectorEvent(
    GameEventBase BaseEvent,
    Serial ItemId,
    int MapId,
    int SectorX,
    int SectorY
) : IGameEvent
{
    /// <summary>
    /// Creates an item-sector-add event with current timestamp.
    /// </summary>
    public ItemAddedInSectorEvent(Serial itemId, int mapId, int sectorX, int sectorY)
        : this(GameEventBase.CreateNow(), itemId, mapId, sectorX, sectorY) { }
}
