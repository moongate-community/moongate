using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when an item's own fields change — its graphic, hue, name or amount. Not raised when it
/// merely moves: equipping, containing and dropping have their own events and send their own packets.
/// </summary>
public sealed record ItemChangedEvent(Serial Item) : ILoopAffineEvent;
