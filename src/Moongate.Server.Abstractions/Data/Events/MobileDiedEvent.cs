using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record MobileDiedEvent(Serial Mobile, Serial Killer) : ILoopAffineEvent;
