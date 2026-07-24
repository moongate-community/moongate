using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Interfaces.Events;

/// <summary>
/// Marks an event whose subscribers mutate or read world state and therefore must run on the
/// game-loop thread. The event bus decorator routes such an event onto the loop when it is
/// published off it, and guards its handlers against running — or going async — off the loop.
/// </summary>
public interface ILoopAffineEvent : IEvent
{
}
