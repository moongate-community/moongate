using Moongate.Core.Types;
using Moongate.Server.Services.Commands;

namespace Moongate.Tests.Server.Commands;

public class CommandServiceTests
{
    [Fact]
    public void IsAuthorized_ActorAboveMinLevel_IsTrue()
        => Assert.True(CommandService.IsAuthorized(AccountLevelType.Administrator, AccountLevelType.GrandMaster));

    [Fact]
    public void IsAuthorized_ActorAtMinLevel_IsTrue()
        => Assert.True(CommandService.IsAuthorized(AccountLevelType.GrandMaster, AccountLevelType.GrandMaster));

    [Fact]
    public void IsAuthorized_ActorBelowMinLevel_IsFalse()
        => Assert.False(CommandService.IsAuthorized(AccountLevelType.Player, AccountLevelType.GrandMaster));

    [Fact]
    public void Parse_JustThePrefix_ReturnsEmptyNameAndNoArguments()
    {
        var (name, arguments) = CommandService.Parse(".");

        Assert.Equal(string.Empty, name);
        Assert.Empty(arguments);
    }

    [Fact]
    public void Parse_NameOnly_ReturnsNameAndNoArguments()
    {
        var (name, arguments) = CommandService.Parse(".broadcast");

        Assert.Equal("broadcast", name);
        Assert.Empty(arguments);
    }

    [Fact]
    public void Parse_NameWithArguments_SplitsOnWhitespace()
    {
        var (name, arguments) = CommandService.Parse(".broadcast Server restarting soon");

        Assert.Equal("broadcast", name);
        Assert.Equal(["Server", "restarting", "soon"], arguments);
    }

    [Fact]
    public void Parse_ExtraWhitespaceBetweenTokens_IsIgnored()
    {
        var (name, arguments) = CommandService.Parse(".broadcast   hello    world");

        Assert.Equal("broadcast", name);
        Assert.Equal(["hello", "world"], arguments);
    }
}
