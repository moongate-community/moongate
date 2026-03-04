using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Spatial;

/// <summary>
/// Event emitted when a mobile enters the active world index.
/// </summary>
public readonly record struct MobileAddedInWorldEvent(
    GameEventBase BaseEvent,
    UOMobileEntity Mobile,
    string? BrainId
) : IGameEvent
{
    /// <summary>
    /// Creates a world-add event with current timestamp.
    /// </summary>
    public MobileAddedInWorldEvent(UOMobileEntity mobile, string? brainId = null)
        : this(GameEventBase.CreateNow(), mobile, brainId) { }
}
