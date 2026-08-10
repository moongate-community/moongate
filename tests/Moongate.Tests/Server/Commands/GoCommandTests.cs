using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;

namespace Moongate.Tests.Server.Commands;

/// <summary>
/// Moving a staff character to a coordinate.
/// <para>
/// The command exists because nothing else could reach anywhere. Three features in a row — world
/// decoration, signs and doors — shipped without a single one being looked at in a client, not because
/// they were broken but because a character logs in where it logs in and the world is 6,144 tiles
/// across. This is the tool that makes any of it checkable.
/// </para>
/// </summary>
public class GoCommandTests
{
    private sealed class RecordingMobileService : IMobileService
    {
        public (Serial Mobile, int X, int Y, int Z)? Moved { get; private set; }

        /// <summary>When false it answers like an unknown serial, which is the only failure it has.</summary>
        public bool Succeeds { get; init; } = true;

        public bool Teleport(Serial mobile, int x, int y, int z)
        {
            if (!Succeeds)
            {
                return false;
            }

            Moved = (mobile, x, y, z);

            return true;
        }
    }

    [Fact]
    public void Execute_WithCoordinates_MovesTheActorThere()
    {
        var mobiles = new RecordingMobileService();
        var actor = new MobileEntity { Id = (Serial)7u };
        var replies = new List<string>();

        new GoCommand(mobiles).Execute(Context(actor, ["3586", "2587"], replies));

        Assert.Equal(((Serial)7u, 3586, 2587, 0), mobiles.Moved);
        Assert.Contains("3586, 2587, 0", replies[^1]);
    }

    // Z matters more than it looks: a door on a second floor is at the same x,y as the ground.
    [Fact]
    public void Execute_WithAZ_UsesIt()
    {
        var mobiles = new RecordingMobileService();

        new GoCommand(mobiles).Execute(Context(new() { Id = (Serial)7u }, ["3592", "2590", "20"], []));

        Assert.Equal(20, mobiles.Moved!.Value.Z);
    }

    [Fact]
    public void Execute_NoArguments_RepliesUsageAndMovesNobody()
    {
        var mobiles = new RecordingMobileService();
        var replies = new List<string>();

        new GoCommand(mobiles).Execute(Context(new(), [], replies));

        Assert.Null(mobiles.Moved);
        Assert.Contains("Usage", Assert.Single(replies));
    }

    [Theory, InlineData("x", "2587"), InlineData("3586", "y"), InlineData("3586", "2587", "z")]
    public void Execute_ACoordinateThatIsNotANumber_SaysSoAndMovesNobody(params string[] arguments)
    {
        var mobiles = new RecordingMobileService();
        var replies = new List<string>();

        new GoCommand(mobiles).Execute(Context(new(), arguments, replies));

        Assert.Null(mobiles.Moved);
        Assert.Contains("number", Assert.Single(replies));
    }

    // A console has no character to move, and saying so beats moving nobody in silence.
    [Fact]
    public void Execute_WithNoActor_SaysItNeedsOne()
    {
        var mobiles = new RecordingMobileService();
        var replies = new List<string>();

        new GoCommand(mobiles).Execute(new(CommandSourceType.Console, null, ["1", "2"], replies.Add));

        Assert.Null(mobiles.Moved);
        Assert.Contains("player", Assert.Single(replies));
    }

    [Fact]
    public void Execute_WhenTheMoveFails_SaysSoRatherThanClaimingSuccess()
    {
        var mobiles = new RecordingMobileService { Succeeds = false };
        var replies = new List<string>();

        new GoCommand(mobiles).Execute(Context(new(), ["1", "2"], replies));

        Assert.DoesNotContain("Moved", Assert.Single(replies));
    }

    private static CommandContext Context(MobileEntity actor, IReadOnlyList<string> arguments, List<string> replies)
        => new(CommandSourceType.InGame, actor, arguments, replies.Add);
}
