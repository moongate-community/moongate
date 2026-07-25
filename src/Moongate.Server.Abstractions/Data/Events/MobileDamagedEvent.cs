using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record MobileDamagedEvent(Serial Mobile, Serial Attacker, int Amount) : ILoopAffineEvent;
