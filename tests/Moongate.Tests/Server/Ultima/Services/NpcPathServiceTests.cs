using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcPathServiceTests
{
    private static readonly Point3D Goal = new(1604, 1600, 0);

    private readonly StubPathfindingService _finder = new();
    private readonly ManualTimeProvider _time = new();
    private readonly NpcPathService _paths;
    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0)
    };

    public NpcPathServiceTests()
    {
        _paths = new(_finder, _time);
    }

    [Fact]
    public void Next_TheFirstTime_SearchesAPartialPath_AndGivesItsFirstStep()
    {
        _finder.Finds(DirectionType.East, DirectionType.East);

        var step = Next();

        Assert.Equal(new NpcPathStep(NpcWalkType.Moving, DirectionType.East), step);
        Assert.Equal(
            (MapType.Trammel, new Point3D(1600, 1600, 0), Goal, MovementAbilityType.Walk, true),
            Assert.Single(_finder.Searches)
        );
    }

    [Fact]
    public void Next_WhileTheStepsAreTaken_FollowsThePath_WithoutSearchingAgain()
    {
        _finder.Finds(DirectionType.East, DirectionType.SouthEast, DirectionType.North);

        var taken = new List<DirectionType>();

        for (var i = 0; i < 3; i++)
        {
            taken.Add(Take());
        }

        Assert.Equal([DirectionType.East, DirectionType.SouthEast, DirectionType.North], taken);
        Assert.Single(_finder.Searches);
    }

    [Theory]
    [InlineData(1604, 1600, 0, true)]
    [InlineData(1603, 1601, 1, true)]
    [InlineData(1602, 1600, 1, false)]
    public void Next_WithinTheRangeOfTheGoal_HasArrived_AndSearchesNothing(int x, int y, int range, bool arrived)
    {
        _orc.Location = new Point3D(x, y, 0);
        _finder.Finds(DirectionType.East);

        var step = _paths.Next(_orc, Goal, range, MovementAbilityType.Walk);

        Assert.Equal(arrived, step.Kind == NpcWalkType.Arrived);
        Assert.Equal(arrived ? 0 : 1, _finder.Searches.Count);
    }

    [Fact]
    public void Next_OnTheGoalsTileAtAnotherHeight_HasNotArrived()
    {
        _orc.Location = new Point3D(1604, 1600, 40);
        _finder.Finds(DirectionType.East);

        Assert.Equal(NpcWalkType.Moving, Next().Kind);
    }

    [Fact]
    public void Next_WhenNoPathIsFound_SaysSo_AndSearchesAgainOnlyAfterTwoSeconds()
    {
        Assert.Equal(NpcWalkType.NoPath, Next().Kind);
        _time.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.Equal(NpcWalkType.NoPath, Next().Kind);
        Assert.Single(_finder.Searches);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        _finder.Finds(DirectionType.East);

        Assert.Equal(NpcWalkType.Moving, Next().Kind);
        Assert.Equal(2, _finder.Searches.Count);
    }

    [Fact]
    public void Stepped_Refused_DropsThePath_AndTheNpcWaitsBeforeSearchingAgain()
    {
        _finder.Finds(DirectionType.East, DirectionType.East);
        Next();

        _paths.Stepped(_orc, false);

        Assert.Equal(NpcWalkType.Blocked, Next().Kind);
        Assert.Single(_finder.Searches);

        _time.Advance(TimeSpan.FromSeconds(2));
        _finder.Finds(DirectionType.South);

        Assert.Equal(new NpcPathStep(NpcWalkType.Moving, DirectionType.South), Next());
    }

    [Fact]
    public void Next_WhenTheGoalMoved_KeepsTheOldPathUntilTwoSecondsPassed_ThenSearchesTheNewOne()
    {
        _finder.Finds(DirectionType.East, DirectionType.East, DirectionType.East);
        Take();
        var moved = new Point3D(1600, 1604, 0);
        _finder.Finds(DirectionType.South);

        var soon = _paths.Next(_orc, moved, 0, MovementAbilityType.Walk);
        _time.Advance(TimeSpan.FromSeconds(2));
        var later = _paths.Next(_orc, moved, 0, MovementAbilityType.Walk);

        Assert.Equal(new NpcPathStep(NpcWalkType.Moving, DirectionType.East), soon);
        Assert.Equal(new NpcPathStep(NpcWalkType.Moving, DirectionType.South), later);
        Assert.Equal(moved, _finder.Searches[^1].To);
    }

    [Fact]
    public void Next_AfterSomethingElseMovedTheNpc_DropsThePath()
    {
        _finder.Finds(DirectionType.East, DirectionType.East);
        Take();
        // A teleporter, or a script's own step.
        _orc.Location = new Point3D(1700, 1700, 0);
        _time.Advance(TimeSpan.FromSeconds(2));
        _finder.Finds(DirectionType.West);

        Assert.Equal(new NpcPathStep(NpcWalkType.Moving, DirectionType.West), Next());
        Assert.Equal(new Point3D(1700, 1700, 0), _finder.Searches[^1].From);
    }

    [Fact]
    public void Next_AtTheEndOfAPartialPath_SearchesAgainWhenItMay()
    {
        _finder.Result = new(PathResultType.Partial, [DirectionType.East], default);
        Take();
        _time.Advance(TimeSpan.FromSeconds(2));
        _finder.Result = new(PathResultType.NotFound, [], default);

        Assert.Equal(NpcWalkType.NoPath, Next().Kind);
        Assert.Equal(2, _finder.Searches.Count);
    }

    [Fact]
    public void Forget_DropsThePathAndItsWait()
    {
        Next();

        _paths.Forget(_orc.Id);
        _finder.Finds(DirectionType.East);

        Assert.Equal(NpcWalkType.Moving, Next().Kind);
    }

    [Fact]
    public void Next_GivesTheSearchTheNpcsAbility()
    {
        _paths.Next(_orc, Goal, 0, MovementAbilityType.Swim);

        Assert.Equal(MovementAbilityType.Swim, Assert.Single(_finder.Searches).Ability);
    }

    private NpcPathStep Next()
    {
        return _paths.Next(_orc, Goal, 0, MovementAbilityType.Walk);
    }

    // Asks for the next step and takes it, as the caller does.
    private DirectionType Take()
    {
        var step = Next();
        Assert.Equal(NpcWalkType.Moving, step.Kind);
        var next = _orc.Location.Move(step.Direction);
        _orc.Location = new Point3D(next.X, next.Y, _orc.Location.Z);
        _paths.Stepped(_orc, true);

        return step.Direction;
    }
}
