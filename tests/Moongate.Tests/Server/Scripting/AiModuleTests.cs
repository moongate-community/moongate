using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Scripting;

namespace Moongate.Tests.Server.Scripting;

public class AiModuleTests
{
    private sealed class RecordingAiActionService : IAiActionService
    {
        public bool Result { get; set; }

        public List<string> Said { get; } = [];
        public List<Serial> Engaged { get; } = [];
        public List<Serial> MovedToward { get; } = [];
        public List<Serial> MovedAway { get; } = [];
        public List<DirectionType> Stepped { get; } = [];
        public List<(int X, int Y)> MovedTo { get; } = [];
        public int Patrols { get; private set; }
        public int ReturnsHome { get; private set; }
        public int ClearsTarget { get; private set; }

        private sealed class NoopScope : IDisposable
        {
            public void Dispose() { }
        }

        public IDisposable Begin(BrainContext context)
            => new NoopScope();

        public bool ClearTarget()
        {
            ClearsTarget++;

            return Result;
        }

        public bool Engage(Serial targetId)
        {
            Engaged.Add(targetId);

            return Result;
        }

        public bool MoveAway(Serial targetId)
        {
            MovedAway.Add(targetId);

            return Result;
        }

        public bool MoveTo(int x, int y)
        {
            MovedTo.Add((x, y));

            return Result;
        }

        public bool MoveToward(Serial targetId)
        {
            MovedToward.Add(targetId);

            return Result;
        }

        public bool Patrol()
        {
            Patrols++;

            return Result;
        }

        public bool ReturnHome()
        {
            ReturnsHome++;

            return Result;
        }

        public bool Say(string text)
        {
            Said.Add(text);

            return Result;
        }

        public bool Step(DirectionType direction)
        {
            Stepped.Add(direction);

            return Result;
        }
    }

    [Fact]
    public void Engage_RoutesTargetSerialToService()
    {
        var actions = new RecordingAiActionService { Result = true };
        var module = new AiModule(actions);

        Assert.True(module.Engage(0x2A));
        Assert.Equal(new(0x2A), Assert.Single(actions.Engaged));
    }

    [Fact]
    public void MoveTo_RoutesCoordinatesToService()
    {
        var actions = new RecordingAiActionService { Result = true };
        var module = new AiModule(actions);

        Assert.True(module.MoveTo(42, 7));
        Assert.Equal((42, 7), Assert.Single(actions.MovedTo));
    }

    [Fact]
    public void MoveToward_And_MoveAway_RouteTargetSerials()
    {
        var actions = new RecordingAiActionService { Result = true };
        var module = new AiModule(actions);

        Assert.True(module.MoveToward(0x3));
        Assert.True(module.MoveAway(0x4));

        Assert.Equal(new(0x3), Assert.Single(actions.MovedToward));
        Assert.Equal(new(0x4), Assert.Single(actions.MovedAway));
    }

    [Fact]
    public void ParameterlessActions_RouteToService()
    {
        var actions = new RecordingAiActionService { Result = false };
        var module = new AiModule(actions);

        Assert.False(module.Patrol());
        Assert.False(module.ReturnHome());
        Assert.False(module.ClearTarget());

        Assert.Equal(1, actions.Patrols);
        Assert.Equal(1, actions.ReturnsHome);
        Assert.Equal(1, actions.ClearsTarget);
    }

    [Fact]
    public void Say_RoutesTextToService()
    {
        var actions = new RecordingAiActionService { Result = true };
        var module = new AiModule(actions);

        Assert.True(module.Say("hello"));
        Assert.Equal("hello", Assert.Single(actions.Said));
    }

    [Theory, InlineData("north", DirectionType.North), InlineData("N", DirectionType.North),
     InlineData("ne", DirectionType.NorthEast), InlineData("southwest", DirectionType.SouthWest),
     InlineData(" NW ", DirectionType.NorthWest)]
    public void Step_ParsesDirectionAndRoutesToService(string direction, DirectionType expected)
    {
        var actions = new RecordingAiActionService { Result = true };
        var module = new AiModule(actions);

        Assert.True(module.Step(direction));
        Assert.Equal(expected, Assert.Single(actions.Stepped));
    }

    [Theory, InlineData(""), InlineData("up"), InlineData("nne")]
    public void Step_UnknownDirection_ReturnsFalseWithoutCallingService(string direction)
    {
        var actions = new RecordingAiActionService { Result = true };
        var module = new AiModule(actions);

        Assert.False(module.Step(direction));
        Assert.Empty(actions.Stepped);
    }
}
