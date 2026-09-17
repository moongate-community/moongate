using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Commands;

namespace Moongate.Tests.Server.Services.Commands;

public sealed class CommandSystemServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ResolvesAliasesCaseInsensitivelyAndPassesArguments()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>(
            "echo|e",
            source: CommandSourceType.Console,
            minimumAccountType: AccountType.Regular
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var output = await service.ExecuteAsync("  E   hello   world  ");

        Assert.Equal("ok", Assert.Single(output).Text);
        var invocation = Assert.Single(container.Resolve<RecordingCommandExecutor>().Invocations);
        Assert.Equal("e", invocation.CommandName);
        Assert.Equal(["hello", "world"], invocation.Arguments);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_BlankInputProducesNoOutput()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        Assert.Empty(await service.ExecuteAsync("   "));
        Assert.Empty(await service.ExecuteAsync(""));

        Assert.Empty(container.Resolve<RecordingCommandExecutor>().Invocations);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommandReportsOneErrorLine()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var line = Assert.Single(await service.ExecuteAsync("nope arg"));

        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Equal("Unknown command: nope", line.Text);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_SourceNotAllowedReportsOneErrorLineAndSkipsTheHandler()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>(
            "echo",
            source: CommandSourceType.Console,
            minimumAccountType: AccountType.Regular
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var line = Assert.Single(await service.ExecuteAsync("echo hi", CommandSourceType.InGame));

        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Equal("Command 'echo' is not available from source 'InGame'.", line.Text);
        Assert.Empty(container.Resolve<RecordingCommandExecutor>().Invocations);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_NoneSourceIsRejectedEvenWhenEverySourceIsAllowed()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>(
            "echo",
            source: CommandSourceType.Console | CommandSourceType.InGame,
            minimumAccountType: AccountType.Regular
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var line = Assert.Single(await service.ExecuteAsync("echo hi", CommandSourceType.None));

        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Empty(container.Resolve<RecordingCommandExecutor>().Invocations);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ConsoleSourceResolvesToAdministrator()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>(
            "echo",
            source: CommandSourceType.Console,
            minimumAccountType: AccountType.Administrator
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var output = await service.ExecuteAsync("echo hi");
        Assert.Equal("ok", Assert.Single(output).Text);

        var session = new GameSession(new NetworkSession(fixture.Client), fixture.Loop);
        var outputWithSession = await service.ExecuteAsync("echo hi", CommandSourceType.Console, session);
        Assert.Equal("ok", Assert.Single(outputWithSession).Text);

        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_InGameWithoutSessionResolvesToRegular()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>(
            "echo",
            source: CommandSourceType.InGame,
            minimumAccountType: AccountType.GameMaster
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var line = Assert.Single(await service.ExecuteAsync("echo hi", CommandSourceType.InGame));

        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Equal("Command 'echo' requires account type 'GameMaster'.", line.Text);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_InGameSessionBelowMinimumIsRejectedAndAtMinimumIsAllowed()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>(
            "echo",
            source: CommandSourceType.InGame,
            minimumAccountType: AccountType.GameMaster
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();
        var session = new GameSession(new NetworkSession(fixture.Client), fixture.Loop);

        var rejected = Assert.Single(await service.ExecuteAsync("echo hi", CommandSourceType.InGame, session));
        Assert.Equal(CommandOutputLevel.Error, rejected.Level);

        await fixture.ExecuteOnLoopAsync(() => session.SetAccountType(AccountType.GameMaster));
        var allowed = Assert.Single(await service.ExecuteAsync("echo hi", CommandSourceType.InGame, session));

        Assert.Equal("ok", allowed.Text);
        Assert.Same(session, Assert.Single(container.Resolve<RecordingCommandExecutor>().Invocations).Session);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_InGameGameMasterBelowAdministratorMinimumIsRejected()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>(
            "echo",
            source: CommandSourceType.InGame,
            minimumAccountType: AccountType.Administrator
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();
        var session = new GameSession(new NetworkSession(fixture.Client), fixture.Loop);
        await fixture.ExecuteOnLoopAsync(() => session.SetAccountType(AccountType.GameMaster));

        var line = Assert.Single(await service.ExecuteAsync("echo hi", CommandSourceType.InGame, session));

        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Empty(container.Resolve<RecordingCommandExecutor>().Invocations);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingHandlerReportsOneErrorLineWithoutPropagating()
    {
        using var container = new Container();
        container.RegisterCommand<ThrowingCommandExecutor>("boom", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var line = Assert.Single(await service.ExecuteAsync("boom"));

        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Equal("Command 'boom' failed. Check logs for details.", line.Text);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancelledTokenThrowsBeforeTheHandlerRuns()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync("echo hi", CommandSourceType.Console, null, cancellation.Token)
        );

        Assert.Empty(container.Resolve<RecordingCommandExecutor>().Invocations);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_BeforeStartAndAfterStopIsRejected()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("echo hi"));

        await service.StartAsync();
        await service.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("echo hi"));
    }

    [Fact]
    public async Task StartAsync_AfterStopIsRejected()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();
        await service.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync());
    }

    [Fact]
    public async Task GetRegisteredCommands_DeduplicatesAliasesAndOrdersByName()
    {
        using var container = new Container();
        container.RegisterCommand<ThrowingCommandExecutor>("zulu|z", minimumAccountType: AccountType.Regular);
        container.RegisterCommand<RecordingCommandExecutor>("alpha|a", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        var definitions = service.GetRegisteredCommands();

        Assert.Equal(new[] { "alpha", "zulu" }, definitions.Select(definition => definition.Name));
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_DispatchesEveryAliasToTheSameExecutor()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo|e", minimumAccountType: AccountType.Regular);
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        await service.ExecuteAsync("echo one");
        await service.ExecuteAsync("e two");

        var executor = container.Resolve<RecordingCommandExecutor>();
        Assert.Equal(2, executor.Invocations.Count);
        Assert.Same(executor, container.Resolve<RecordingCommandExecutor>());
        await service.StopAsync();
    }
}
