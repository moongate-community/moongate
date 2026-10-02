using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     A* over a square window of tiles between start and goal, one node per tile: a straight step costs 10 and a
///     diagonal 14, as ModernUO, UOX3 and Sphere, and the octile distance to the goal guides the search, so the path
///     found is the shortest. The node arrays are kept between searches: the game loop runs one search at a time.
/// </summary>
public sealed class PathfindingService : IPathfindingService
{
    private const int StraightCost = 10;
    private const int DiagonalCost = 14;

    // A goal is reached within a mover's height of it, as ModernUO's planes and POL's goal test.
    private const int MoverHeight = 16;

    private const byte Unseen = 0;
    private const byte Open = 1;
    private const byte Closed = 2;
    private const int NoParent = -1;

    private readonly IMovementService _movement;
    private readonly WorldConfig _world;
    private readonly PriorityQueue<int, int> _open = new();

    private int[] _cost = [];
    private int[] _parent = [];
    private int[] _height = [];
    private byte[] _state = [];
    private byte[] _step = [];

    public PathfindingService(IMovementService movement, WorldConfig world)
    {
        _movement = movement;
        _world = world;
    }

    public PathResult FindPath(
        MapType map,
        Point3D from,
        Point3D to,
        MovementAbilityType ability = MovementAbilityType.Walk,
        bool allowPartial = false
    )
    {
        var distanceX = Math.Abs(to.X - from.X);
        var distanceY = Math.Abs(to.Y - from.Y);
        var range = _world.PathfindingRange;

        if (distanceX > range || distanceY > range)
        {
            return new(PathResultType.TooFar, [], from);
        }

        if (distanceX == 0 && distanceY == 0)
        {
            return new(Math.Abs(to.Z - from.Z) <= MoverHeight ? PathResultType.Found : PathResultType.NotFound, [], from);
        }

        // Both ends fit, with the room left over shared between the two sides.
        var side = range + 1;
        var originX = Math.Min(from.X, to.X) - (range - distanceX) / 2;
        var originY = Math.Min(from.Y, to.Y) - (range - distanceY) / 2;
        Reset(side * side);

        var start = from.X - originX + (from.Y - originY) * side;
        _cost[start] = 0;
        _parent[start] = NoParent;
        _height[start] = from.Z;
        _state[start] = Open;
        _open.Enqueue(start, Heuristic(from.X, from.Y, to));

        // The expanded node closest to the goal, for a partial path.
        var closest = start;
        var closestDistance = Heuristic(from.X, from.Y, to);
        var expanded = 0;

        try
        {
            while (_open.TryDequeue(out var node, out _))
            {
                // A node is queued again each time a shorter way to it is found: the later entries are stale.
                if (_state[node] == Closed)
                {
                    continue;
                }

                _state[node] = Closed;
                var x = originX + node % side;
                var y = originY + node / side;
                var z = _height[node];

                if (x == to.X && y == to.Y && Math.Abs(z - to.Z) <= MoverHeight)
                {
                    return Build(PathResultType.Found, node, new Point3D(x, y, z));
                }

                var distance = Heuristic(x, y, to);

                if (distance < closestDistance)
                {
                    closest = node;
                    closestDistance = distance;
                }

                if (expanded == _world.PathfindingMaxNodes)
                {
                    break;
                }

                expanded++;
                var here = new Point3D(x, y, z);

                for (var direction = 0; direction < 8; direction++)
                {
                    var next = here.Move((DirectionType)direction);
                    var column = next.X - originX;
                    var row = next.Y - originY;

                    if (column < 0 || row < 0 || column >= side || row >= side)
                    {
                        continue;
                    }

                    var neighbour = column + row * side;

                    if (_state[neighbour] == Closed ||
                        !_movement.CheckMovement(map, here, (DirectionType)direction, ability, out var landing))
                    {
                        continue;
                    }

                    var cost = _cost[node] + ((direction & 1) == 0 ? StraightCost : DiagonalCost);

                    if (_state[neighbour] == Open && cost >= _cost[neighbour])
                    {
                        continue;
                    }

                    _cost[neighbour] = cost;
                    _parent[neighbour] = node;
                    _height[neighbour] = landing;
                    _step[neighbour] = (byte)direction;
                    _state[neighbour] = Open;
                    _open.Enqueue(neighbour, cost + Heuristic(next.X, next.Y, to));
                }
            }
        }
        catch (KeyNotFoundException)
        {
            // The map is not loaded: there is nowhere to walk.
            return new(PathResultType.NotFound, [], from);
        }

        if (allowPartial && closest != start)
        {
            return Build(
                PathResultType.Partial,
                closest,
                new Point3D(originX + closest % side, originY + closest / side, _height[closest])
            );
        }

        return new(PathResultType.NotFound, [], from);
    }

    // The octile distance: diagonal steps while both differences last, straight ones for the rest.
    private static int Heuristic(int x, int y, Point3D to)
    {
        var distanceX = Math.Abs(to.X - x);
        var distanceY = Math.Abs(to.Y - y);

        return StraightCost * (distanceX + distanceY) - (2 * StraightCost - DiagonalCost) * Math.Min(distanceX, distanceY);
    }

    private void Reset(int nodes)
    {
        if (_state.Length < nodes)
        {
            _cost = new int[nodes];
            _parent = new int[nodes];
            _height = new int[nodes];
            _state = new byte[nodes];
            _step = new byte[nodes];
        }
        else
        {
            Array.Clear(_state, 0, nodes);
        }

        _open.Clear();
    }

    // The steps from the start to the node, read back along the parents.
    private PathResult Build(PathResultType kind, int node, Point3D end)
    {
        var steps = new List<DirectionType>();

        for (var current = node; _parent[current] != NoParent; current = _parent[current])
        {
            steps.Add((DirectionType)_step[current]);
        }

        steps.Reverse();

        return new(kind, steps, end);
    }
}
