using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record MobileDeletedEvent(MobileEntity Mobile) : ILoopAffineEvent;
