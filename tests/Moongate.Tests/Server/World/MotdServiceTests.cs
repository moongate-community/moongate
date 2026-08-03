using SquidStd.Core.Directories;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.World;

/// <summary>
/// The greeting a player meets on the way in: what the shard is running, how many people are here,
/// and whatever the operator wrote in motd.txt.
/// </summary>
public class MotdServiceTests : IDisposable
{
    private readonly string _root = TemporaryDirectory.Create("mg-motd-");

    [Fact]
    public void Lines_OpenWithTheShardVersion()
    {
        var motd = new MotdService(Directories(), "0.4.2", () => 0);

        Assert.StartsWith("Moongate v0.4.2", motd.Lines()[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Lines_ReportTheOnlineCount()
    {
        var motd = new MotdService(Directories(), "0.4.2", () => 3);

        Assert.Equal("Total online players: 3", motd.Lines()[1]);
    }

    // The count is read when a player is greeted, not when the service is built — otherwise every
    // greeting after the first would report the number of people online at startup.
    [Fact]
    public void Lines_ReadTheCountEachTime()
    {
        var online = 1;
        var motd = new MotdService(Directories(), "0.4.2", () => online);

        Assert.Equal("Total online players: 1", motd.Lines()[1]);

        online = 7;

        Assert.Equal("Total online players: 7", motd.Lines()[1]);
    }

    [Fact]
    public void Lines_CarryWhatTheOperatorWrote()
    {
        File.WriteAllText(MotdPath(), "Welcome to Britannia!");

        var lines = new MotdService(Directories(), "0.4.2", () => 0).Lines();

        Assert.Equal("Welcome to Britannia!", lines[2]);
    }

    // An operator writes a paragraph, not a sentence. Each line is its own system message, because
    // the client shows one line at a time.
    [Fact]
    public void Lines_SplitTheFileIntoOneMessagePerLine()
    {
        File.WriteAllText(MotdPath(), "First line\nSecond line\nThird line");

        var lines = new MotdService(Directories(), "0.4.2", () => 0).Lines();

        Assert.Equal(["First line", "Second line", "Third line"], lines.Skip(2));
    }

    // A file that is edited and left with trailing newlines must not greet anyone with blank lines.
    [Fact]
    public void Lines_DropBlankLinesFromTheFile()
    {
        File.WriteAllText(MotdPath(), "Welcome!\n\n\nCome back soon.\n\n");

        var lines = new MotdService(Directories(), "0.4.2", () => 0).Lines();

        Assert.Equal(["Welcome!", "Come back soon."], lines.Skip(2));
    }

    // First boot: the operator should find the file and edit it, rather than having to learn it
    // could exist. Same courtesy moongate.yaml already gets.
    [Fact]
    public void Lines_WriteASampleFileWhenThereIsNone()
    {
        var lines = new MotdService(Directories(), "0.4.2", () => 0).Lines();

        Assert.True(File.Exists(MotdPath()));
        Assert.Equal(3, lines.Count);
        Assert.NotEmpty(lines[2]);
    }

    // The operator emptied it on purpose. Two lines, and no invented third.
    [Fact]
    public void Lines_AreJustTheTwoWhenTheFileIsEmpty()
    {
        File.WriteAllText(MotdPath(), "   \n\n");

        var lines = new MotdService(Directories(), "0.4.2", () => 0).Lines();

        Assert.Equal(2, lines.Count);
    }

    private string MotdPath()
        => Path.Combine(_root, "motd.txt");

    private DirectoriesConfig Directories()
        => new(_root, []);

    public void Dispose()
        => TemporaryDirectory.Remove(_root);
}
