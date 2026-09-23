using DryIoc;
using Moongate.Server.Commands;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Commands;

namespace Moongate.Tests.Server.Commands;

public sealed class HelpCommandTests
{
    [Fact]
    public async Task Help_ConsoleListsEachAvailableCommandOnce()
    {
        using var container = CreateContainer();
        var commands = await StartAsync(container);

        var lines = await commands.ExecuteAsync("help");

        Assert.Equal(
            ["Available commands:", "admin - Administrator action", "console-only - Console action",
             "echo - Echoes arguments", "help - Lists commands"],
            lines.Select(line => line.Text).ToArray()
        );
        await commands.StopAsync();
    }

    [Fact]
    public async Task Help_AliasShowsCanonicalCommandDetails()
    {
        using var container = CreateContainer();
        var commands = await StartAsync(container);

        var lines = await commands.ExecuteAsync("help e");

        Assert.Equal(
            ["Command: echo", "Description: Echoes arguments", "Aliases: echo, e",
             "Sources: InGame, Console", "Minimum account level: Regular"],
            lines.Select(line => line.Text).ToArray()
        );
        await commands.StopAsync();
    }

    [Fact]
    public async Task Help_InGameRegularAccountHidesConsoleAndAdministratorCommands()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var commands = await StartAsync(container);
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);

        var lines = await commands.ExecuteAsync("help", CommandSourceType.InGame, session);
        var hidden = await commands.ExecuteAsync("help admin", CommandSourceType.InGame, session);

        Assert.Equal(["Available commands:", "echo - Echoes arguments", "help - Lists commands"],
            lines.Select(line => line.Text).ToArray());
        Assert.Equal(CommandOutputLevel.Error, Assert.Single(hidden).Level);
        Assert.Equal("Unknown or unavailable command: admin", hidden[0].Text);
        await commands.StopAsync();
    }

    [Theory, InlineData("help missing", "Unknown or unavailable command: missing"),
     InlineData("help echo extra", "Usage: help [command]")]
    public async Task Help_InvalidRequestPrintsOneError(string input, string expected)
    {
        using var container = CreateContainer();
        var commands = await StartAsync(container);

        var line = Assert.Single(await commands.ExecuteAsync(input));

        Assert.Equal(expected, line.Text);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        await commands.StopAsync();
    }

    private static Container CreateContainer()
    {
        var container = new Container();
        container.RegisterCommand<HelpCommand>("help", "Lists commands", CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Regular);
        container.RegisterCommand<EchoCommand>("echo|e", "Echoes arguments", CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Regular);
        container.RegisterCommand<RecordingCommandExecutor>("admin", "Administrator action",
            CommandSourceType.Console | CommandSourceType.InGame, AccountType.Administrator);
        container.RegisterCommand<RecordingCommandExecutor>("console-only", "Console action", CommandSourceType.Console,
            AccountType.Regular);

        return container;
    }

    private static async Task<CommandSystemService> StartAsync(Container container)
    {
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        return service;
    }
}
