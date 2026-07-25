using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record MobileAttackedEvent(Serial Defender, Serial Attacker) : ILoopAffineEvent;
