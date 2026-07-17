using Moongate.Core.Primitives;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a player double-clicks a mobile. Carries only the clicking session and the target
/// serial; resolving the mobile and deciding what to do is left to subscribers.
/// </summary>
public sealed record MobileDoubleClickEvent(long SessionId, Serial Serial) : IEvent;
