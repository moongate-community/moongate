using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Tests.Server.Core.Commands;

public sealed class CommandContextTests
{
    [Fact]
    public void Print_WithoutArgumentsLeavesBracesUnformatted()
    {
        var context = CreateContext();

        context.Print("literal {0} braces");

        var line = Assert.Single(context.Output);
        Assert.Equal("literal {0} braces", line.Text);
        Assert.Equal(CommandOutputLevel.Information, line.Level);
    }

    [Fact]
    public void Print_WithArgumentsFormatsTheMessage()
    {
        var context = CreateContext();

        context.Print("hello {0} and {1}", "world", 42);

        Assert.Equal("hello world and 42", Assert.Single(context.Output).Text);
    }

    [Fact]
    public void Print_SingleLineEmptyMessageProducesOneEmptyLine()
    {
        var context = CreateContext();

        context.Print("");

        Assert.Equal("", Assert.Single(context.Output).Text);
    }

    [Fact]
    public void Print_MultiLineMessageSplitsAndDropsBlankLines()
    {
        var context = CreateContext();

        context.Print("first\r\n\r\nsecond\n");

        Assert.Collection(
            context.Output,
            line => Assert.Equal("first", line.Text),
            line => Assert.Equal("second", line.Text)
        );
    }

    [Fact]
    public void PrintWarningAndPrintError_CarryTheirOwnLevels()
    {
        var context = CreateContext();

        context.PrintWarning("careful");
        context.PrintError("broken");

        Assert.Collection(
            context.Output,
            line => Assert.Equal(CommandOutputLevel.Warning, line.Level),
            line => Assert.Equal(CommandOutputLevel.Error, line.Level)
        );
    }

    [Fact]
    public void Constructor_ExposesParsedInvocation()
    {
        var context = CreateContext();

        Assert.Equal("echo hi there", context.CommandLine);
        Assert.Equal("echo", context.CommandName);
        Assert.Equal(["hi", "there"], context.Arguments);
        Assert.Equal(CommandSourceType.Console, context.Source);
        Assert.Null(context.Session);
        Assert.False(context.IsInGame);
        Assert.Empty(context.Output);
    }

    private static CommandContext CreateContext()
    {
        return new CommandContext("echo hi there", "echo", ["hi", "there"], CommandSourceType.Console, null);
    }
}
