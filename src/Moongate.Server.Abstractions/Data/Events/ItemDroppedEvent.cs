using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a player puts down an item they were holding. <paramref name="ContainerId" /> is the
/// container it went into, or <see cref="Serial.Zero" /> when it was dropped on the ground.
/// </summary>
public sealed record ItemDroppedEvent(Serial Item, Serial Actor, Serial ContainerId) : ILoopAffineEvent;
