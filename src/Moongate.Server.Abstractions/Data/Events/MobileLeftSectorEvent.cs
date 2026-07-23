using Moongate.Core.Primitives;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a mobile leaves a spatial-index sector — either being removed from the world or crossing
/// a sector boundary into another one. Fires only on sector boundaries, never per tile, and only for
/// mobiles (not items). Auto-exposed to Lua as <c>events.on("mobile_left_sector", ...)</c>, with a table
/// carrying <c>mobile</c>, <c>map_id</c>, <c>sector_x</c> and <c>sector_y</c>.
/// </summary>
public sealed record MobileLeftSectorEvent(Serial Mobile, int MapId, int SectorX, int SectorY) : IEvent;
