using Moongate.Server.Services.Console.Internal;

namespace Moongate.Tests.Server.Services.Console.Internal;

public sealed class ConsoleCompletionTests
{
    private static readonly string[] Names = ["save", "sql_backup", "shutdown", "help", "?", "spawns"];

    [Fact]
    public void OneMatch_CompletesTheNameAndASpace()
    {
        var (text, matches) = ConsoleCompletion.Complete("he", Names);

        Assert.Equal("help ", text);
        Assert.Equal(["help"], matches);
    }

    [Fact]
    public void SeveralMatches_CompleteTheirCommonPrefix_AndAreListedInOrder()
    {
        var (text, matches) = ConsoleCompletion.Complete("s", ["save", "spawns", "spawn"]);

        Assert.Equal("s", text);
        Assert.Equal(["save", "spawn", "spawns"], matches);

        Assert.Equal("spawn", ConsoleCompletion.Complete("sp", ["spawns", "spawn"]).Text);
    }

    [Fact]
    public void TheTypedCase_DoesNotMatter()
    {
        Assert.Equal("help ", ConsoleCompletion.Complete("HE", Names).Text);
    }

    [Fact]
    public void NoMatch_LeavesTheLine()
    {
        var (text, matches) = ConsoleCompletion.Complete("xyz", Names);

        Assert.Equal(("xyz", 0), (text, matches.Count));
    }

    [Theory, InlineData("save now"), InlineData("help ")]
    public void PastTheFirstWord_NothingIsCompleted(string line)
    {
        var (text, matches) = ConsoleCompletion.Complete(line, Names);

        Assert.Equal((line, 0), (text, matches.Count));
    }

    [Fact]
    public void AnEmptyLine_ListsEveryName_AndCompletesNothing()
    {
        var (text, matches) = ConsoleCompletion.Complete("", ["save", "help"]);

        Assert.Equal("", text);
        Assert.Equal(["help", "save"], matches);
    }

    [Fact]
    public void ANameListedTwice_CountsOnce()
    {
        Assert.Equal("help ", ConsoleCompletion.Complete("h", ["help", "help"]).Text);
    }
}
