using Moongate.Core.Primitives;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a player single-clicks an item. Carries only the clicking session and the target
/// serial; resolving the item and deciding what to do is left to subscribers.
/// </summary>
public sealed record ItemSingleClickEvent(long SessionId, Serial Serial) : IEvent;
