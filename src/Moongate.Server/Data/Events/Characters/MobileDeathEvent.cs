using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Raised when a mobile death is resolved and any corpse has been created.
/// </summary>
public readonly record struct MobileDeathEvent(
    GameEventBase BaseEvent,
    UOMobileEntity Victim,
    UOMobileEntity? Killer,
    UOItemEntity? Corpse,
    string? RegionName
) : IGameEvent
{
    public MobileDeathEvent(UOMobileEntity victim, UOMobileEntity? killer, UOItemEntity? corpse, string? regionName)
        : this(GameEventBase.CreateNow(), victim, killer, corpse, regionName) { }
}
