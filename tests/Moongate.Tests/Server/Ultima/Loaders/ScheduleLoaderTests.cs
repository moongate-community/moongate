using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class ScheduleLoaderTests
{
    [Fact]
    public async Task NoFile_IsAnEmptyCalendar()
    {
        using var directory = new TemporaryDirectory();

        var result = await new ScheduleLoader(new DirectoriesConfig(directory.Path, ["data"])).LoadDataAsync();

        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task AFileOfComments_IsAnEmptyCalendar()
    {
        var file = await Load("# nothing here\n");

        Assert.Empty(file.Task);
        Assert.Empty(file.Event);
    }

    [Fact]
    public async Task AValidFile_IsLoadedWithEveryField()
    {
        var file = await Load(
            """
            [[task]]
            id = "nightly_restart"
            when = { every = "day", at = "04:00" }
            action = "shutdown"
            warnings = [600, 300, 60, 10]

            [[task]]
            id = "sunday_tip"
            when = { every = "week", days = ["sun", "sat"], at = "18:00" }
            action = "broadcast"
            message = 30230

            [[task]]
            id = "hourly"
            when = { every = "hour", at = ":30" }
            action = "broadcast"
            text = "Visit the bank."

            [[task]]
            id = "cleanup"
            when = { every = "day", at = "06:00" }
            action = "lua"
            script = "cleanup"
            function = "tick"

            [[event]]
            id = "halloween"
            name = "Halloween"
            from = "10-20"
            to = "11-02"
            """
        );

        Assert.Equal(4, file.Task.Count);
        Assert.Equal(new[] { 600, 300, 60, 10 }, file.Task[0].Warnings);
        Assert.Equal(("day", "04:00"), (file.Task[0].When.Every, file.Task[0].When.At));
        Assert.Equal(("week", "18:00"), (file.Task[1].When.Every, file.Task[1].When.At));
        Assert.Equal(new[] { "sun", "sat" }, file.Task[1].When.Days);
        Assert.Equal(30230, file.Task[1].Message);
        Assert.Equal(("hour", ":30", "Visit the bank."), (file.Task[2].When.Every, file.Task[2].When.At, file.Task[2].Text));
        Assert.Equal(("lua", "cleanup", "tick"), (file.Task[3].Action, file.Task[3].Script, file.Task[3].Function));
        Assert.Equal(
            ("halloween", "Halloween", "10-20", "11-02"),
            (file.Event[0].Id, file.Event[0].Name, file.Event[0].From, file.Event[0].To)
        );
    }

    [Fact]
    public async Task ALeapDay_IsAValidDateOfAWindow()
    {
        var file = await Load("[[event]]\nid = \"leap\"\nname = \"Leap\"\nfrom = \"02-29\"\nto = \"03-01\"\n");

        Assert.Single(file.Event);
    }

    [Theory]
    [InlineData("[[task]]\nid = \"A\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"", "task 'A'", "id")]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"month\", at = \"04:00\" }\naction = \"shutdown\"",
        "task 'x'",
        "every"
    )]
    [InlineData("[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"24:00\" }\naction = \"shutdown\"", "task 'x'", "at")]
    [InlineData("[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"4:00\" }\naction = \"shutdown\"", "task 'x'", "at")]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"hour\", at = \"04:00\" }\naction = \"shutdown\"",
        "task 'x'",
        "at"
    )]
    [InlineData("[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \":30\" }\naction = \"shutdown\"", "task 'x'", "at")]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"week\", days = [\"funday\"], at = \"04:00\" }\naction = \"shutdown\"",
        "task 'x'",
        "days"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"week\", days = [\"sun\", \"sun\"], at = \"04:00\" }\naction = \"shutdown\"",
        "task 'x'",
        "days"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", days = [\"sun\"], at = \"04:00\" }\naction = \"shutdown\"",
        "task 'x'",
        "days"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"reboot\"",
        "task 'x'",
        "action"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\nwarnings = [10, 60]",
        "task 'x'",
        "warnings"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\nwarnings = [60, 60]",
        "task 'x'",
        "warnings"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\nwarnings = [0]",
        "task 'x'",
        "warnings"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\nwarnings = [86401]",
        "task 'x'",
        "warnings"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"broadcast\"\ntext = \"a\"\nwarnings = [60]",
        "task 'x'",
        "warnings"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"broadcast\"",
        "task 'x'",
        "message"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"broadcast\"\nmessage = 5\ntext = \"a\"",
        "task 'x'",
        "message"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"broadcast\"\nmessage = 0",
        "task 'x'",
        "message"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\ntext = \"a\"",
        "task 'x'",
        "text"
    )]
    [InlineData("[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"lua\"", "task 'x'", "script")]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"lua\"\nscript = \"../x\"",
        "task 'x'",
        "script"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"lua\"\nscript = \"cleanup\"\nfunction = \"Run!\"",
        "task 'x'",
        "function"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\nscript = \"cleanup\"",
        "task 'x'",
        "script"
    )]
    [InlineData(
        "[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\n[[task]]\nid = \"x\"\nwhen = { every = \"day\", at = \"05:00\" }\naction = \"shutdown\"",
        "x",
        "twice"
    )]
    [InlineData(
        "[[task]]\nid = \"e\"\nwhen = { every = \"day\", at = \"04:00\" }\naction = \"shutdown\"\n[[event]]\nid = \"e\"\nname = \"E\"\nfrom = \"10-01\"\nto = \"10-02\"",
        "e",
        "twice"
    )]
    [InlineData("[[event]]\nid = \"e\"\nname = \"E\"\nfrom = \"13-01\"\nto = \"01-02\"", "event 'e'", "from")]
    [InlineData("[[event]]\nid = \"e\"\nname = \"E\"\nfrom = \"02-30\"\nto = \"03-02\"", "event 'e'", "from")]
    [InlineData("[[event]]\nid = \"e\"\nname = \"\"\nfrom = \"10-01\"\nto = \"10-02\"", "event 'e'", "name")]
    [InlineData("[[event]]\nid = \"e\"\nname = \"E\"\nfrom = \"10-01\"\nto = \"1002\"", "event 'e'", "to")]
    [InlineData("[[event]]\nname = \"E\"\nfrom = \"10-01\"\nto = \"10-02\"", "event ''", "id")]
    public async Task ABadEntry_StopsTheStartup_NamingTheEntryAndTheField(string toml, string entry, string field)
    {
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => Load(toml));

        Assert.Contains("schedule.toml", exception.Message);
        Assert.Contains(entry, exception.Message);
        Assert.Contains(field, exception.Message);
    }

    private static async Task<ScheduleFile> Load(string toml)
    {
        using var directory = new TemporaryDirectory();
        directory.CreateFile("data/schedule.toml", toml);

        var result = await new ScheduleLoader(new DirectoriesConfig(directory.Path, ["data"])).LoadDataAsync();

        return Assert.Single(result.Entities);
    }
}
