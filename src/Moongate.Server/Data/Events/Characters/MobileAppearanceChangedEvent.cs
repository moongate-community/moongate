using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Raised when a mobile appearance changes in place and clients must redraw it without movement.
/// </summary>
public readonly record struct MobileAppearanceChangedEvent(
    GameEventBase BaseEvent,
    UOMobileEntity Mobile
) : IGameEvent
{
    public MobileAppearanceChangedEvent(UOMobileEntity mobile)
        : this(GameEventBase.CreateNow(), mobile) { }
}
