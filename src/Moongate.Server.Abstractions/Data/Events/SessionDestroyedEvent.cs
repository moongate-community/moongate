using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>Raised when a client disconnects and its <see cref="PlayerSession" /> is removed.</summary>
public sealed record SessionDestroyedEvent(PlayerSession Session) : ILoopAffineEvent;
