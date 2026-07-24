using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>Raised when a client connects and its <see cref="PlayerSession" /> is created.</summary>
public sealed record SessionCreatedEvent(PlayerSession Session) : ILoopAffineEvent;
