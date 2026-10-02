using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Data.Movement;

/// <summary>
///     What a search for a path found: how it ended, the steps to take from the start, one direction each, and the
///     place they lead to, which is the start itself when there are none.
/// </summary>
public sealed record PathResult(PathResultType Kind, IReadOnlyList<DirectionType> Steps, Point3D End);
