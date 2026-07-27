using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record SectorDeactivatedEvent(int MapId, int SectorX, int SectorY) : ILoopAffineEvent;
