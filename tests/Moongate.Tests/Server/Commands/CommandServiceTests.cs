using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Commands;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Commands;

public class CommandServiceTests
{
    private sealed class RecordingCommand : ICommand
    {
        public CommandContext? LastContext { get; private set; }

        public void Execute(CommandContext context)
            => LastContext = context;
    }

    private static CommandRegistration Registration(
        string name,
        CommandSourceType sources = CommandSourceType.InGame,
        AccountLevelType minLevel = AccountLevelType.GrandMaster,
        RecordingCommand? command = null
    )
        => new(name, minLevel, "A recording test command.", sources, _ => command ?? new RecordingCommand());

    private static CommandService Service(params CommandRegistration[] registrations)
        => new(registrations, new Container(), new StubAccountService());

    // ---- BuildRegistry (unchanged behavior) ----

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

    // ---- Parse (now prefix-free) ----

    [Fact]
    public void Parse_ExtraWhitespaceBetweenTokens_IsIgnored()
    {
        var (name, arguments) = CommandService.Parse("broadcast   hello    world");

        Assert.Equal("broadcast", name);
        Assert.Equal(["hello", "world"], arguments);
    }

    [Fact]
    public void Parse_EmptyLine_ReturnsEmptyNameAndNoArguments()
    {
        var (name, arguments) = CommandService.Parse(string.Empty);

        Assert.Equal(string.Empty, name);
        Assert.Empty(arguments);
    }

    [Fact]
    public void Parse_NameWithArguments_SplitsOnWhitespace()
    {
        var (name, arguments) = CommandService.Parse("broadcast Server restarting soon");

        Assert.Equal("broadcast", name);
        Assert.Equal(["Server", "restarting", "soon"], arguments);
    }

    // ---- IsAuthorized ----

    [Fact]
    public void IsAuthorized_ActorAtOrAboveMinLevel_IsTrue()
    {
        Assert.True(CommandService.IsAuthorized(AccountLevelType.GrandMaster, AccountLevelType.GrandMaster));
        Assert.True(CommandService.IsAuthorized(AccountLevelType.Administrator, AccountLevelType.GrandMaster));
    }

    [Fact]
    public void IsAuthorized_ActorBelowMinLevel_IsFalse()
        => Assert.False(CommandService.IsAuthorized(AccountLevelType.Player, AccountLevelType.GrandMaster));

    // ---- Execute(CommandInvocation): gating ----

    [Fact]
    public void Execute_KnownAllowedAuthorized_DispatchesWithArguments()
    {
        var command = new RecordingCommand();
        var service = Service(Registration("test|t", CommandSourceType.Console, AccountLevelType.GrandMaster, command));

        service.Execute(
            new CommandInvocation(
                CommandSourceType.Console,
                AccountLevelType.GrandMaster,
                null,
                "test a b",
                _ => { }
            )
        );

        Assert.NotNull(command.LastContext);
        Assert.Equal(["a", "b"], command.LastContext!.Value.Arguments);
        Assert.Equal(CommandSourceType.Console, command.LastContext!.Value.Source);
    }

    [Fact]
    public void Execute_UnknownName_RepliesUnknownCommand()
    {
        var replies = new List<string>();
        var service = Service(Registration("test", CommandSourceType.Console));

        service.Execute(
            new CommandInvocation(
                CommandSourceType.Console,
                AccountLevelType.Administrator,
                null,
                "nope",
                replies.Add
            )
        );

        Assert.Equal("Unknown command.", Assert.Single(replies));
    }

    [Fact]
    public void Execute_SourceNotAllowed_RepliesUnknownCommand()
    {
        var replies = new List<string>();
        var service = Service(Registration("test", CommandSourceType.InGame)); // in-game only

        service.Execute(
            new CommandInvocation(
                CommandSourceType.Console,
                AccountLevelType.Administrator,
                null,
                "test",
                replies.Add
            )
        );

        Assert.Equal("Unknown command.", Assert.Single(replies));
    }

    [Fact]
    public void Execute_LevelBelowMinimum_RepliesUnknownCommand()
    {
        var replies = new List<string>();
        var service = Service(Registration("test", CommandSourceType.Console, AccountLevelType.GrandMaster));

        service.Execute(
            new CommandInvocation(
                CommandSourceType.Console,
                AccountLevelType.Player,
                null,
                "test",
                replies.Add
            )
        );

        Assert.Equal("Unknown command.", Assert.Single(replies));
    }

    // ---- ListCommands ----

    [Fact]
    public void ListCommands_ReturnsOnlyCommandsForThatSource_WithCanonicalName()
    {
        var service = Service(
            Registration("broadcast|bc", CommandSourceType.InGame | CommandSourceType.Console),
            Registration("ingameonly", CommandSourceType.InGame)
        );

        var console = service.ListCommands(CommandSourceType.Console);

        var descriptor = Assert.Single(console);
        Assert.Equal("broadcast", descriptor.Name);
        Assert.Equal(AccountLevelType.GrandMaster, descriptor.MinLevel);
    }
}
