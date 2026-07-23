using Moongate.Core.Primitives;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a mobile enters a spatial-index sector — either appearing in the world or crossing a
/// sector boundary from another one. Fires only on sector boundaries, never per tile, and only for
/// mobiles (not items). Auto-exposed to Lua as <c>events.on("mobile_entered_sector", ...)</c>, with a
/// table carrying <c>mobile</c>, <c>map_id</c>, <c>sector_x</c> and <c>sector_y</c>.
/// </summary>
public sealed record MobileEnteredSectorEvent(Serial Mobile, int MapId, int SectorX, int SectorY) : IEvent;
