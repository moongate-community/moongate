using Moongate.Server.Services.Console.Internal;

namespace Moongate.Tests.Server.Services.Console.Internal;

public sealed class ConsoleCompletionTests
{
    private static readonly string[] Names = ["save", "sql_backup", "shutdown", "help", "?", "spawns", "account"];

    [Fact]
    public void OneMatch_CompletesTheNameAndASpace()
    {
        var (text, matches) = Complete("he");

        Assert.Equal("help ", text);
        Assert.Equal(["help"], matches);
    }

    [Fact]
    public void SeveralMatches_CompleteTheirCommonPrefix_AndAreListedInOrder()
    {
        var (text, matches) = ConsoleCompletion.Complete("s", _ => ["save", "spawns", "spawn"]);

        Assert.Equal("s", text);
        Assert.Equal(["save", "spawn", "spawns"], matches);

        Assert.Equal("spawn", ConsoleCompletion.Complete("sp", _ => ["spawns", "spawn"]).Text);
    }

    [Fact]
    public void TheTypedCase_DoesNotMatter()
    {
        Assert.Equal("help ", Complete("HE").Text);
    }

    [Fact]
    public void NoMatch_LeavesTheLine()
    {
        var (text, matches) = Complete("xyz");

        Assert.Equal(("xyz", 0), (text, matches.Count));
    }

    [Fact]
    public void AnEmptyLine_ListsEveryName_AndCompletesNothing()
    {
        var (text, matches) = ConsoleCompletion.Complete("", _ => ["save", "help"]);

        Assert.Equal("", text);
        Assert.Equal(["help", "save"], matches);
    }

    [Fact]
    public void ANameListedTwice_CountsOnce()
    {
        Assert.Equal("help ", ConsoleCompletion.Complete("h", _ => ["help", "help"]).Text);
    }

    [Fact]
    public void SpacesBeforeTheName_AreKept()
    {
        Assert.Equal("  help ", Complete("  he").Text);
    }

    [Fact]
    public void TheCommonPrefix_TakesTheCaseOfTheNames()
    {
        Assert.Equal("save", ConsoleCompletion.Complete("SA", _ => ["save", "saveall"]).Text);
    }

    [Fact]
    public void AnArgument_IsCompletedFromTheCandidatesOfTheWordsBeforeIt()
    {
        IReadOnlyList<string>? asked = null;

        var (text, matches) = ConsoleCompletion.Complete(
            "account  cr",
            previous =>
            {
                asked = previous;

                return ["create", "api-access"];
            }
        );

        Assert.Equal(["account"], asked);
        Assert.Equal("account  create ", text);
        Assert.Equal(["create"], matches);
    }

    [Fact]
    public void AfterASpace_TheNextArgumentStartsEmpty_AndEveryCandidateMatches()
    {
        IReadOnlyList<string>? asked = null;

        var (text, matches) = ConsoleCompletion.Complete(
            "account create bob secret ",
            previous =>
            {
                asked = previous;

                return ["Regular", "GameMaster", "Administrator"];
            }
        );

        Assert.Equal(["account", "create", "bob", "secret"], asked);
        Assert.Equal("account create bob secret ", text);
        Assert.Equal(["Administrator", "GameMaster", "Regular"], matches);
    }

    [Fact]
    public void AnArgumentWithNoCandidates_LeavesTheLine()
    {
        var (text, matches) = Complete("save now");

        Assert.Equal(("save now", 0), (text, matches.Count));
    }

    // The names for the first word, nothing after it.
    private static (string Text, IReadOnlyList<string> Matches) Complete(string line)
    {
        return ConsoleCompletion.Complete(line, previous => previous.Count == 0 ? Names : []);
    }
}
