using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Raised before a mobile death is finalized and before any corpse is created.
/// </summary>
public readonly record struct MobileBeforeDeathEvent(
    GameEventBase BaseEvent,
    UOMobileEntity Victim,
    UOMobileEntity? Killer,
    string? RegionName
) : IGameEvent
{
    public MobileBeforeDeathEvent(UOMobileEntity victim, UOMobileEntity? killer, string? regionName)
        : this(GameEventBase.CreateNow(), victim, killer, regionName) { }
}
