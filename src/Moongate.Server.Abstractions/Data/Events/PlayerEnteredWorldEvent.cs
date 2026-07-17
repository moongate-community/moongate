using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised after a player's enter-world burst has been sent and the session is in the world. Lets
/// systems react to a character coming online (spawns, greetings, presence) without touching the
/// network layer.
/// </summary>
public sealed record PlayerEnteredWorldEvent(long SessionId, Serial AccountId, MobileEntity Mobile) : IEvent;
