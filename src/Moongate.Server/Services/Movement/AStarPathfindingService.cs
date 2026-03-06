using Moongate.Server.Interfaces.Services.Movement;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Movement;

/// <summary>
/// A* pathfinding implementation that uses movement validation for walkability checks.
/// </summary>
public sealed class AStarPathfindingService : IPathfindingService
{
    private const int CardinalCost = 10;
    private const int DiagonalCost = 14;
    private const int AdditionalSearchMargin = 16;

    private static readonly DirectionType[] Directions =
    [
        DirectionType.North,
        DirectionType.NorthEast,
        DirectionType.East,
        DirectionType.SouthEast,
        DirectionType.South,
        DirectionType.SouthWest,
        DirectionType.West,
        DirectionType.NorthWest
    ];

    private readonly IMovementValidationService _movementValidationService;

    public AStarPathfindingService(IMovementValidationService movementValidationService)
    {
        _movementValidationService = movementValidationService;
    }

    public bool TryFindPath(
        UOMobileEntity mobile,
        Point3D targetLocation,
        out IReadOnlyList<DirectionType> path,
        int maxVisitedNodes = 1024
    )
    {
        ArgumentNullException.ThrowIfNull(mobile);
        path = [];

        var start = mobile.Location;

        if (start == targetLocation)
        {
            return true;
        }

        var clampedMaxVisitedNodes = Math.Clamp(maxVisitedNodes, 64, 100_000);
        var maxSearchDelta = Math.Max(Math.Abs(targetLocation.X - start.X), Math.Abs(targetLocation.Y - start.Y)) +
                             AdditionalSearchMargin;

        var open = new PriorityQueue<Point3D, int>();
        var cameFrom = new Dictionary<Point3D, (Point3D Previous, DirectionType Direction)>();
        var gScore = new Dictionary<Point3D, int> { [start] = 0 };
        var closed = new HashSet<Point3D>();
        var visitedNodes = 0;

        open.Enqueue(start, Heuristic(start, targetLocation));

        while (open.Count > 0)
        {
            var current = open.Dequeue();

            if (!closed.Add(current))
            {
                continue;
            }

            if (++visitedNodes > clampedMaxVisitedNodes)
            {
                return false;
            }

            if (current == targetLocation || current.X == targetLocation.X && current.Y == targetLocation.Y)
            {
                path = ReconstructPath(cameFrom, current);

                return true;
            }

            foreach (var direction in Directions)
            {
                if (!TryResolveNeighbor(mobile, current, direction, out var neighbor))
                {
                    continue;
                }

                if (Math.Abs(neighbor.X - start.X) > maxSearchDelta ||
                    Math.Abs(neighbor.Y - start.Y) > maxSearchDelta)
                {
                    continue;
                }

                if (closed.Contains(neighbor))
                {
                    continue;
                }

                var currentScore = gScore[current];
                var tentative = currentScore + MoveCost(direction);

                if (gScore.TryGetValue(neighbor, out var knownScore) && tentative >= knownScore)
                {
                    continue;
                }

                cameFrom[neighbor] = (current, direction);
                gScore[neighbor] = tentative;
                var fScore = tentative + Heuristic(neighbor, targetLocation);
                open.Enqueue(neighbor, fScore);
            }
        }

        return false;
    }

    private static int Heuristic(Point3D from, Point3D to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        var diagonal = Math.Min(dx, dy);
        var straight = Math.Abs(dx - dy);

        return diagonal * DiagonalCost + straight * CardinalCost;
    }

    private static int MoveCost(DirectionType direction)
    {
        var baseDirection = Point3D.GetBaseDirection(direction);

        return baseDirection is DirectionType.NorthEast or
                                DirectionType.SouthEast or
                                DirectionType.SouthWest or
                                DirectionType.NorthWest
                   ? DiagonalCost
                   : CardinalCost;
    }

    private static IReadOnlyList<DirectionType> ReconstructPath(
        IReadOnlyDictionary<Point3D, (Point3D Previous, DirectionType Direction)> cameFrom,
        Point3D current
    )
    {
        var reversedDirections = new List<DirectionType>();

        while (cameFrom.TryGetValue(current, out var edge))
        {
            reversedDirections.Add(edge.Direction);
            current = edge.Previous;
        }

        reversedDirections.Reverse();

        return reversedDirections;
    }

    private bool TryResolveNeighbor(UOMobileEntity sourceMobile, Point3D from, DirectionType direction, out Point3D to)
    {
        var probe = new UOMobileEntity
        {
            Id = sourceMobile.Id,
            MapId = sourceMobile.MapId,
            Location = from,
            Direction = direction
        };

        return _movementValidationService.TryResolveMove(probe, direction, out to);
    }
}
