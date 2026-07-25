using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record SectorActivatedEvent(int MapId, int SectorX, int SectorY) : ILoopAffineEvent;
