using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Raised when a mobile toggles war mode and nearby players must receive an update.
/// </summary>
public readonly record struct MobileWarModeChangedEvent(
    GameEventBase BaseEvent,
    UOMobileEntity Mobile
) : IGameEvent
{
    public MobileWarModeChangedEvent(UOMobileEntity mobile)
        : this(new(), mobile) { }
}
