using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Commands;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Server;

public class CommandRestSourceTests
{
    private sealed class Echo : ICommand
    {
        public void Execute(CommandContext context)
            => context.Reply("ran");
    }

    private static ICommandService Build(CommandSourceType sources)
    {
        var registration = new CommandRegistration(
            "echo",
            AccountLevelType.GrandMaster,
            "Echo.",
            sources,
            _ => new Echo()
        );

        return new CommandService([registration], new Container(), new SeededAccountService());
    }

    [Fact]
    public void Rest_flag_is_distinct()
        => Assert.Equal(4, (byte)CommandSourceType.Rest);

    [Fact]
    public void Execute_runs_a_rest_enabled_command_from_the_rest_source()
    {
        var lines = new List<string>();

        Build(CommandSourceType.Console | CommandSourceType.Rest)
            .Execute(new CommandInvocation(CommandSourceType.Rest, AccountLevelType.GrandMaster, null, "echo", lines.Add));

        Assert.Contains("ran", lines);
    }

    [Fact]
    public void Execute_refuses_a_console_only_command_from_the_rest_source()
    {
        var lines = new List<string>();

        Build(CommandSourceType.Console)
            .Execute(new CommandInvocation(CommandSourceType.Rest, AccountLevelType.GrandMaster, null, "echo", lines.Add));

        Assert.DoesNotContain("ran", lines);
        Assert.Contains("Unknown command.", lines);
    }
}
