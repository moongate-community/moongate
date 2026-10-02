using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Types.Decorations;

namespace Moongate.Server.Ultima.Data.Decorations;

/// <summary>
///     A door the map's door frames call for: where it stands closed and which way it hangs.
/// </summary>
public readonly record struct GeneratedDoor(Point3D Location, DoorFacingType Facing);
