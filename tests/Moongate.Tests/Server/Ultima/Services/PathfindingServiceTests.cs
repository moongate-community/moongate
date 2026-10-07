using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PathfindingServiceTests
{
    private readonly GridMovementService _movement = new();
    private readonly WorldConfig _world = new();
    private readonly PathfindingService _paths;

    public PathfindingServiceTests()
    {
        _paths = new(_movement, _world);
    }

    [Fact]
    public void FindPath_OnOpenGround_GoesStraight()
    {
        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.Equal(Enumerable.Repeat(DirectionType.East, 4), path.Steps);
        Assert.Equal(new Point3D(104, 100, 0), path.End);
    }

    [Fact]
    public void FindPath_OnOpenGround_TakesTheDiagonalsFirstOrLast_NeverMoreStepsThanNeeded()
    {
        var path = Find(new Point3D(100, 100, 0), new Point3D(103, 105, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        // Three diagonal steps and two straight ones: the larger of the two differences.
        Assert.Equal(5, path.Steps.Count);
        Assert.Equal(3, path.Steps.Count(step => step == DirectionType.SouthEast));
        Assert.Equal(new Point3D(103, 105, 0), Walk(new Point3D(100, 100, 0), path.Steps));
    }

    [Fact]
    public void FindPath_ToWhereItStands_IsFoundWithNoSteps()
    {
        var path = Find(new Point3D(100, 100, 0), new Point3D(100, 100, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.Empty(path.Steps);
        Assert.Equal(0, _movement.Checks);
    }

    [Fact]
    public void FindPath_AroundAWall_GoesThroughItsGap()
    {
        // A wall across the whole window with a gap at y 107.
        _movement.Wall(102, 60, 102, 106).Wall(102, 108, 102, 140);

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        var cells = Cells(new Point3D(100, 100, 0), path.Steps);
        Assert.Contains(new Point3D(102, 107, 0), cells);
        Assert.DoesNotContain(cells, cell => _movement.Walls.Contains((cell.X, cell.Y)));
        Assert.Equal(new Point3D(104, 100, 0), cells[^1]);
    }

    [Fact]
    public void FindPath_IsTheShortest_NotTheOneThatLooksNearest()
    {
        // A wall with two gaps: one four tiles north of the straight line, one twelve tiles south. A dead-end pocket
        // right in front of the goal draws a search that only looks at the distance.
        _movement.Wall(104, 85, 104, 95).Wall(104, 97, 104, 111).Wall(104, 113, 104, 120);
        _movement.Wall(101, 99, 103, 99).Wall(101, 101, 103, 101);

        var path = Find(new Point3D(98, 100, 0), new Point3D(108, 100, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        var cells = Cells(new Point3D(98, 100, 0), path.Steps);
        Assert.Contains(new Point3D(104, 96, 0), cells);
        // Four diagonals and two straight steps to the gap, which a diagonal cannot enter or leave past the wall's
        // ends: one step out of it, three diagonals and one step down.
        Assert.Equal(4 * 10 + 7 * 14, Cost(path.Steps));
    }

    [Fact]
    public void FindPath_ACheaperWayFoundLater_ReplacesTheFirst()
    {
        // The tile east of the start is first reached around a pillar's corner; the straight step is checked after it.
        _movement.Walls.Add((100, 99));

        var path = Find(new Point3D(100, 100, 0), new Point3D(102, 98, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.Equal(10 + 14 + 10, Cost(path.Steps));
    }

    [Fact]
    public void FindPath_UsesTheRoomBehindTheStart()
    {
        // Walled on three sides: the only way out is west, away from the goal, then around.
        _movement.Wall(101, 97, 101, 103).Wall(98, 97, 101, 97).Wall(98, 103, 101, 103);

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.Contains(Cells(new Point3D(100, 100, 0), path.Steps), cell => cell.X < 98);
    }

    [Fact]
    public void FindPath_AGoalOnAnUpperFloor_IsReachedByItsStairs_NotStoppedUnderIt()
    {
        // A balcony over the goal's tile, with walkable ground under it, reached by a solid ramp that climbs two units a
        // tile along x 109, from the ground at y 110 up to y 100.
        _movement.Floors[(110, 100)] = 20;

        for (var step = 0; step <= 10; step++)
        {
            _movement.Heights[(109, 100 + step)] = 20 - 2 * step;
        }

        var path = Find(new Point3D(100, 100, 0), new Point3D(110, 100, 20));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.Equal(new Point3D(110, 100, 20), path.End);
        Assert.Equal(new Point3D(110, 100, 20), Cells(new Point3D(100, 100, 0), path.Steps)[^1]);
    }

    [Fact]
    public void FindPath_NearTheEdgeOfTheMap_StaysOnIt()
    {
        var path = Find(new Point3D(1, 2, 0), new Point3D(5, 0, 0));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.Equal(new Point3D(5, 0, 0), Walk(new Point3D(1, 2, 0), path.Steps));
    }

    [Theory]
    [InlineData(3, PathResultType.NotFound)]
    [InlineData(4, PathResultType.Found)]
    public void FindPath_ReachesAGoalFoundJustAsTheLimitIsMet(int limit, PathResultType expected)
    {
        _world.PathfindingMaxNodes = limit;

        Assert.Equal(expected, Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0)).Kind);
    }

    [Fact]
    public void FindPath_NeverCutsACorner()
    {
        _movement.Walls.Add((101, 100));

        var path = Find(new Point3D(100, 100, 0), new Point3D(101, 101, 0));

        // Not the diagonal past the wall: down, then right.
        Assert.Equal([DirectionType.South, DirectionType.East], path.Steps);
    }

    [Fact]
    public void FindPath_KeepsTheHeightOfEveryStep_AndEndsAtTheGoalsHeight()
    {
        _movement.Heights[(101, 100)] = 2;
        _movement.Heights[(102, 100)] = 4;

        var path = Find(new Point3D(100, 100, 0), new Point3D(102, 100, 4));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.Equal([DirectionType.East, DirectionType.East], path.Steps);
        Assert.Equal(new Point3D(102, 100, 4), path.End);
    }

    [Fact]
    public void FindPath_AStepTooHigh_IsWalkedAround()
    {
        // A cliff straight ahead, a ramp to the south.
        _movement.Heights[(102, 100)] = 10;
        _movement.Heights[(102, 101)] = 2;
        _movement.Heights[(103, 101)] = 4;
        _movement.Heights[(103, 100)] = 6;

        var path = Find(new Point3D(101, 100, 0), new Point3D(103, 100, 6));

        Assert.Equal(PathResultType.Found, path.Kind);
        Assert.DoesNotContain(new Point3D(102, 100, 10), Cells(new Point3D(101, 100, 0), path.Steps));
    }

    [Fact]
    public void FindPath_AGoalAtAnotherHeightOfItsTile_IsNotReached()
    {
        _movement.Heights[(101, 100)] = 0;

        var path = Find(new Point3D(100, 100, 0), new Point3D(101, 100, 40));

        Assert.Equal(PathResultType.NotFound, path.Kind);
    }

    [Fact]
    public void FindPath_AWalledInGoal_IsNotFound_AndGivesNoSteps()
    {
        _movement.Wall(103, 99, 105, 99).Wall(103, 101, 105, 101).Wall(103, 100, 103, 100).Wall(105, 100, 105, 100);

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.Equal(PathResultType.NotFound, path.Kind);
        Assert.Empty(path.Steps);
        Assert.Equal(new Point3D(100, 100, 0), path.End);
    }

    [Fact]
    public void FindPath_AWalledInGoal_WithPartial_LeadsToTheClosestTile()
    {
        _movement.Wall(103, 99, 105, 99).Wall(103, 101, 105, 101).Wall(103, 100, 103, 100).Wall(105, 100, 105, 100);

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0), true);

        Assert.Equal(PathResultType.Partial, path.Kind);
        Assert.Equal(new Point3D(102, 100, 0), path.End);
        Assert.Equal(path.End, Walk(new Point3D(100, 100, 0), path.Steps));
    }

    [Fact]
    public void FindPath_WithPartial_WhenNothingIsCloserThanTheStart_IsNotFound()
    {
        // The start itself is walled in.
        _movement.Wall(99, 99, 101, 99).Wall(99, 101, 101, 101).Wall(99, 100, 99, 100).Wall(101, 100, 101, 100);

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0), true);

        Assert.Equal(PathResultType.NotFound, path.Kind);
        Assert.Empty(path.Steps);
    }

    [Theory]
    [InlineData(138, 100, PathResultType.Found)]
    [InlineData(139, 100, PathResultType.TooFar)]
    [InlineData(100, 61, PathResultType.TooFar)]
    public void FindPath_AGoalBeyondTheRange_IsTooFar_AndSearchesNothing(int x, int y, PathResultType expected)
    {
        var path = Find(new Point3D(100, 100, 0), new Point3D(x, y, 0));

        Assert.Equal(expected, path.Kind);

        if (expected == PathResultType.TooFar)
        {
            Assert.Equal(0, _movement.Checks);
        }
    }

    [Fact]
    public void FindPath_StopsAtTheNodeLimit()
    {
        _world.PathfindingMaxNodes = 5;
        // A long wall: going around takes far more than five expansions.
        _movement.Wall(102, 85, 102, 115);

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.Equal(PathResultType.NotFound, path.Kind);
        // Five expansions, at most eight neighbours each.
        Assert.InRange(_movement.Checks, 1, 40);
    }

    [Fact]
    public void FindPath_StaysInsideItsWindow()
    {
        // The only way round passes far outside the window between start and goal.
        _movement.Wall(102, 0, 102, 400);

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.Equal(PathResultType.NotFound, path.Kind);
        Assert.True(_movement.Checks <= 8 * 39 * 39, $"{_movement.Checks} checks");
    }

    [Fact]
    public void FindPath_OnAMapThatIsNotLoaded_IsNotFound()
    {
        _movement.ThrowMapNotLoaded = true;

        var path = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.Equal(PathResultType.NotFound, path.Kind);
    }

    [Fact]
    public void FindPath_Again_ForgetsThePreviousSearch()
    {
        _movement.Wall(102, 95, 102, 106);
        var first = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));
        _movement.Walls.Clear();

        var second = Find(new Point3D(100, 100, 0), new Point3D(104, 100, 0));

        Assert.True(first.Steps.Count > 4);
        Assert.Equal(Enumerable.Repeat(DirectionType.East, 4), second.Steps);
    }

    [Fact]
    public void FindPath_GivesTheMovementTheAbilityItWasAsked()
    {
        var movement = new StubMovementService();

        new PathfindingService(movement, _world).FindPath(
            MapType.Trammel,
            new Point3D(100, 100, 0),
            new Point3D(101, 100, 0),
            MovementAbilityType.Swim
        );

        Assert.All(movement.Abilities, ability => Assert.Equal(MovementAbilityType.Swim, ability));
        Assert.NotEmpty(movement.Abilities);
    }

    [Fact]
    public void FindPath_OverTheRealMovement_GoesAroundAClosedDoor_AndThroughItForWhoPassesDoors()
    {
        // A wall of crates across a 16x16 map with a closed door in it and a gap at its far end.
        var map = new FakeMapService(16, 16);
        var tiles = new FakeTileDataService()
            .Item(0x0E3D, TileFlagType.Impassable, 10)
            .Item(0x0675, TileFlagType.Impassable | TileFlagType.Door, 20);
        var sectors = TestSectors.Create();
        uint serial = 0x40000001;

        for (var y = 0; y <= 13; y++)
        {
            var item = new ItemEntity
                { Id = new Serial(serial++), TemplateId = "thing", ItemId = y == 5 ? 0x0675 : 0x0E3D, Amount = 1 };
            item.PlaceOnGround(MapType.Felucca, new Point3D(8, y, 0));
            sectors.AddItem(item);
        }

        var paths = new PathfindingService(new MovementService(map, tiles, sectors), _world);

        var walker = paths.FindPath(MapType.Felucca, new Point3D(6, 5, 0), new Point3D(10, 5, 0));
        var staff = paths.FindPath(
            MapType.Felucca,
            new Point3D(6, 5, 0),
            new Point3D(10, 5, 0),
            MovementAbilityType.Walk | MovementAbilityType.PassDoors
        );

        Assert.Equal(PathResultType.Found, walker.Kind);
        // Down to the gap at y 14 and back up.
        Assert.True(walker.Steps.Count > 15, $"{walker.Steps.Count} steps");
        Assert.Equal(Enumerable.Repeat(DirectionType.East, 4), staff.Steps);
    }

    private PathResult Find(Point3D from, Point3D to, bool allowPartial = false)
    {
        return _paths.FindPath(MapType.Trammel, from, to, MovementAbilityType.Walk, allowPartial);
    }

    private static int Cost(IReadOnlyList<DirectionType> steps)
    {
        return steps.Sum(step => ((byte)step & 1) == 0 ? 10 : 14);
    }

    private Point3D Walk(Point3D from, IReadOnlyList<DirectionType> steps)
    {
        return steps.Count == 0 ? from : Cells(from, steps)[^1];
    }

    // The cells a walker passes, each at the height the movement lands it.
    private List<Point3D> Cells(Point3D from, IReadOnlyList<DirectionType> steps)
    {
        var cells = new List<Point3D>();
        var here = from;

        foreach (var step in steps)
        {
            Assert.True(
                _movement.CheckMovement(MapType.Trammel, here, step, MovementAbilityType.Walk, out var z),
                $"{step} from {here}"
            );
            var next = here.Move(step);
            here = new Point3D(next.X, next.Y, z);
            cells.Add(here);
        }

        return cells;
    }
}
