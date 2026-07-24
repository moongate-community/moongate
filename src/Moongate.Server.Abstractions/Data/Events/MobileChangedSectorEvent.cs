using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a mobile moves from one spatial-index sector to another (never on spawn or removal — those
/// are covered by <see cref="MobileEnteredSectorEvent" /> and <see cref="MobileLeftSectorEvent" />, both
/// of which also fire alongside this one on a move). Carries the full source and target sector keys so a
/// map change is unambiguous. Auto-exposed to Lua as <c>events.on("mobile_changed_sector", ...)</c>, with
/// a table carrying <c>mobile</c>, <c>from_map_id</c>, <c>from_sector_x</c>, <c>from_sector_y</c>,
/// <c>to_map_id</c>, <c>to_sector_x</c> and <c>to_sector_y</c>.
/// </summary>
public sealed record MobileChangedSectorEvent(
    Serial Mobile,
    int FromMapId,
    int FromSectorX,
    int FromSectorY,
    int ToMapId,
    int ToSectorX,
    int ToSectorY
) : ILoopAffineEvent;
