using Moongate.Server.Data.Session;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Data.Events;

/// <summary>Raised when a client connects and its <see cref="PlayerSession" /> is created.</summary>
public sealed record SessionCreatedEvent(PlayerSession Session) : IEvent;
