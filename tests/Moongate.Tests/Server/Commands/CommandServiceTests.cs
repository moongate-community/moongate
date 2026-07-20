using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Commands;

namespace Moongate.Tests.Server.Commands;

public class CommandServiceTests
{
    private sealed class RecordingCommand : ICommand
    {
        public CommandContext? LastContext { get; private set; }

        public void Execute(CommandContext context)
            => LastContext = context;
    }

    private static CommandRegistration Registration(string name)
        => new(
            name,
            AccountLevelType.GrandMaster,
            "A recording test command.",
            CommandSourceType.InGame,
            static _ => new RecordingCommand()
        );

    [Fact]
    public void BuildRegistry_LookupIsCaseInsensitive()
    {
        var registry = CommandService.BuildRegistry([Registration("test|t")]);

        Assert.True(registry.ContainsKey("TEST"));
        Assert.True(registry.ContainsKey("Test"));
    }

    [Fact]
    public void BuildRegistry_PipeDelimitedAliases_RegisterBothNamesToTheSameRegistration()
    {
        var registration = Registration("test|t");

        var registry = CommandService.BuildRegistry([registration]);

        Assert.Same(registration, registry["test"]);
        Assert.Same(registration, registry["t"]);
    }

    [Fact]
    public void BuildRegistry_SingleCommand_RegistersItsCanonicalNameAndMetadata()
    {
        var registry = CommandService.BuildRegistry([Registration("test|t")]);

        Assert.True(registry.ContainsKey("test"));
        Assert.Equal(AccountLevelType.GrandMaster, registry["test"].MinLevel);
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
    public void Parse_ExtraWhitespaceBetweenTokens_IsIgnored()
    {
        var (name, arguments) = CommandService.Parse(".broadcast   hello    world");

        Assert.Equal("broadcast", name);
        Assert.Equal(["hello", "world"], arguments);
    }

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
}
