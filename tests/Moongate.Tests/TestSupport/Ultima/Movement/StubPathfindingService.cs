using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Movement;

/// <summary>
///     Answers every search with <see cref="Result" /> and records what was asked.
/// </summary>
public sealed class StubPathfindingService : IPathfindingService
{
    public PathResult Result { get; set; } = new(PathResultType.NotFound, [], default);

    public List<(MapType Map, Point3D From, Point3D To, MovementAbilityType Ability, bool AllowPartial)> Searches { get; } = [];

    /// <summary>
    ///     Makes the next searches find these steps.
    /// </summary>
    public void Finds(params DirectionType[] steps)
    {
        Result = new(PathResultType.Found, steps, default);
    }

    public PathResult FindPath(
        MapType map,
        Point3D from,
        Point3D to,
        MovementAbilityType ability = MovementAbilityType.Walk,
        bool allowPartial = false
    )
    {
        Searches.Add((map, from, to, ability, allowPartial));

        return Result;
    }
}
