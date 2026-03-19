using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Raised after a mobile death has been fully applied and post-death side effects are scheduled.
/// </summary>
public readonly record struct MobileAfterDeathEvent(
    GameEventBase BaseEvent,
    UOMobileEntity Victim,
    UOMobileEntity? Killer,
    UOItemEntity? Corpse,
    string? RegionName
) : IGameEvent
{
    public MobileAfterDeathEvent(UOMobileEntity victim, UOMobileEntity? killer, UOItemEntity? corpse, string? regionName)
        : this(GameEventBase.CreateNow(), victim, killer, corpse, regionName) { }
}
