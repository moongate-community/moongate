using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Spatial;

/// <summary>
/// Event emitted when a player exits a region.
/// </summary>
public readonly record struct PlayerExitedRegionEvent(
    GameEventBase BaseEvent,
    Serial MobileId,
    int MapId,
    int RegionId,
    string RegionName
) : IGameEvent
{
    /// <summary>
    /// Creates a region-exit event with current timestamp.
    /// </summary>
    public PlayerExitedRegionEvent(Serial mobileId, int mapId, int regionId, string regionName)
        : this(GameEventBase.CreateNow(), mobileId, mapId, regionId, regionName) { }
}
