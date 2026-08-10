using Moongate.Core.Geometry;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.World;

/// <summary>One doorway the map art draws, and the door that belongs in it.</summary>
/// <param name="MapId">Which facet.</param>
/// <param name="Point">The gap between the two frames.</param>
/// <param name="Facing">Which way the door hangs, which also picks its graphic.</param>
/// <param name="FrameId">The frame graphic that found it — the wall's material, which picks the door.</param>
public readonly record struct DoorPlacement(int MapId, Point3D Point, DoorFacingType Facing, int FrameId);
