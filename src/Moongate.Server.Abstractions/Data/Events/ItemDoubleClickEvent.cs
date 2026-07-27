using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a player double-clicks an item. Carries only the clicking session and the target
/// serial; resolving the item and deciding what to do is left to subscribers.
/// </summary>
public sealed record ItemDoubleClickEvent(long SessionId, Serial Serial) : ILoopAffineEvent;
