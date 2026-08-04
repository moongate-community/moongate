using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.World;

namespace Moongate.Tests.Server.World;

/// <summary>
/// Whether a step is *due*, which is a different question from whether it is *legal* and the only one
/// with any business being forgiving. A hard threshold refuses anything a millisecond early, and
/// network jitter delivers that constantly — each refusal snaps the client back to the server's
/// position, which is what a player feels as stuttering.
/// </summary>
public class MovementThrottleTests
{
    private static readonly DateTimeOffset Due = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan MaxCredit = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void AStepArrivingExactlyOnTime_Runs()
        => Assert.Equal(
            MovementThrottleVerdictType.Run,
            MovementService.Throttle(Due, Due, TimeSpan.Zero, MaxCredit).Verdict
        );

    // The case that made walking stutter: jitter puts a packet a few milliseconds ahead of schedule.
    [Fact]
    public void AStepArrivingSlightlyEarly_Runs_AndSpendsThatMuchSlack()
    {
        var decision = MovementService.Throttle(Due.AddMilliseconds(-5), Due, TimeSpan.Zero, MaxCredit);

        Assert.Equal(MovementThrottleVerdictType.Run, decision.Verdict);
        Assert.Equal(TimeSpan.FromMilliseconds(-5), decision.Credit);
    }

    [Fact]
    public void AStepArrivingLate_Runs_AndRebuildsThatMuchSlack()
    {
        var decision = MovementService.Throttle(
            Due.AddMilliseconds(30),
            Due,
            TimeSpan.FromMilliseconds(-50),
            MaxCredit
        );

        Assert.Equal(MovementThrottleVerdictType.Run, decision.Verdict);
        Assert.Equal(TimeSpan.FromMilliseconds(-20), decision.Credit);
    }

    // Otherwise standing still would bank enough slack to take a dozen steps at once afterwards.
    [Fact]
    public void SlackRebuiltByStandingStill_StopsAtTheCeiling()
    {
        var decision = MovementService.Throttle(Due.AddMinutes(5), Due, TimeSpan.Zero, MaxCredit);

        Assert.Equal(MaxCredit, decision.Credit);
    }

    [Fact]
    public void AStepEarlierThanTheSlackLeft_Waits()
    {
        var decision = MovementService.Throttle(
            Due.AddMilliseconds(-250),
            Due,
            TimeSpan.Zero,
            MaxCredit
        );

        Assert.Equal(MovementThrottleVerdictType.Queue, decision.Verdict);
    }

    /// <summary>Waiting must not also charge for the wait, or the client could never catch up.</summary>
    [Fact]
    public void AStepThatWaits_LeavesTheSlackUntouched()
    {
        var credit = TimeSpan.FromMilliseconds(-100);
        var decision = MovementService.Throttle(Due.AddMilliseconds(-250), Due, credit, MaxCredit);

        Assert.Equal(credit, decision.Credit);
    }

    // Exactly at the debt limit still runs; a millisecond past it waits.
    [Theory]
    [InlineData(-200, MovementThrottleVerdictType.Run)]
    [InlineData(-201, MovementThrottleVerdictType.Queue)]
    public void TheDebtLimitIsTheBoundary(int early, MovementThrottleVerdictType expected)
        => Assert.Equal(
            expected,
            MovementService.Throttle(Due.AddMilliseconds(early), Due, TimeSpan.Zero, MaxCredit).Verdict
        );

    /// <summary>
    /// A sustained early rate drains the slack and then waits, rather than running forever: the
    /// buffer forgives jitter, it does not forgive speed.
    /// </summary>
    [Fact]
    public void ASteadyStreamOfEarlySteps_RunsUntilTheSlackIsGone_ThenWaits()
    {
        var credit = TimeSpan.Zero;
        var ran = 0;

        for (var step = 0; step < 20; step++)
        {
            var decision = MovementService.Throttle(Due.AddMilliseconds(-30), Due, credit, MaxCredit);

            if (decision.Verdict == MovementThrottleVerdictType.Queue)
            {
                break;
            }

            credit = decision.Credit;
            ran++;
        }

        // 200ms of slack at 30ms a step: six run outright, the seventh would owe 210ms and waits.
        Assert.Equal(6, ran);
    }

    [Fact]
    public void WithNoSlackConfigured_AnythingEarlyWaits()
        => Assert.Equal(
            MovementThrottleVerdictType.Queue,
            MovementService.Throttle(Due.AddMilliseconds(-1), Due, TimeSpan.Zero, TimeSpan.Zero).Verdict
        );

    [Fact]
    public void Cost_WalkingIsFourHundredMilliseconds()
        => Assert.Equal(TimeSpan.FromMilliseconds(400), MovementService.CostOf(Facing(DirectionType.East), DirectionType.East));

    [Fact]
    public void Cost_RunningIsTwoHundred()
        => Assert.Equal(
            TimeSpan.FromMilliseconds(200),
            MovementService.CostOf(Facing(DirectionType.East), DirectionType.East | DirectionType.Running)
        );

    // ModernUO's TurnDelay is 0: turning on the spot is free, so a spin never eats the walk budget.
    [Fact]
    public void Cost_TurningOnTheSpotIsFree()
        => Assert.Equal(TimeSpan.Zero, MovementService.CostOf(Facing(DirectionType.East), DirectionType.South));

    private static MobileEntity Facing(DirectionType direction)
        => new() { Id = new(0x1), MapId = 0, Position = new(1, 1, 0), Direction = direction };
}
