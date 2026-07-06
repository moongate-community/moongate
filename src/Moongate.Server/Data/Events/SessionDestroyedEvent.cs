using Moongate.Server.Data.Session;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Data.Events;

/// <summary>Raised when a client disconnects and its <see cref="PlayerSession" /> is removed.</summary>
public sealed record SessionDestroyedEvent(PlayerSession Session) : IEvent;
