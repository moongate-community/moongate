using Moongate.Core.Geometry;

namespace Moongate.UO.Data.World;

/// <summary>One decoration object, resolved to the single spot it occupies on one map.</summary>
/// <param name="Type">What the source declared it as. <c>Static</c> for most.</param>
/// <param name="ItemId">The graphic.</param>
/// <param name="Hue">0 for the raw art.</param>
/// <param name="MapId">Which facet it stands on.</param>
/// <param name="Point">Where it stands.</param>
public readonly record struct DecorationPlacement(string Type, int ItemId, int Hue, int MapId, Point3D Point);
