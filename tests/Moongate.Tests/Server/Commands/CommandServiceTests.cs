using Moongate.Core.Types;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Services.Commands;

namespace Moongate.Tests.Server.Commands;

public class CommandServiceTests
{
    [Command("test|t", AccountLevelType.GrandMaster, "A recording test command.")]
    private sealed class RecordingCommand : ICommand
    {
        public CommandContext? LastContext { get; private set; }

        public void Execute(CommandContext context)
            => LastContext = context;
    }

    private sealed class UndecoratedCommand : ICommand
    {
        public void Execute(CommandContext context) { }
    }

    [Fact]
    public void BuildRegistry_LookupIsCaseInsensitive()
    {
        var command = new RecordingCommand();

        var registry = CommandService.BuildRegistry([command]);

        Assert.True(registry.ContainsKey("TEST"));
        Assert.True(registry.ContainsKey("Test"));
    }

    [Fact]
    public void BuildRegistry_MissingCommandAttribute_Throws()
        => Assert.Throws<InvalidOperationException>(() => CommandService.BuildRegistry([new UndecoratedCommand()]));

    [Fact]
    public void BuildRegistry_PipeDelimitedAliases_RegisterBothNamesToTheSameCommand()
    {
        var command = new RecordingCommand();

        var registry = CommandService.BuildRegistry([command]);

        Assert.Same(command, registry["test"].Command);
        Assert.Same(command, registry["t"].Command);
    }

    [Fact]
    public void BuildRegistry_SingleCommand_RegistersItsCanonicalName()
    {
        var command = new RecordingCommand();

        var registry = CommandService.BuildRegistry([command]);

        Assert.True(registry.ContainsKey("test"));
        Assert.Equal(AccountLevelType.GrandMaster, registry["test"].Attribute.MinLevel);
    }

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
