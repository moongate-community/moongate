using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Tests.TestSupport.Commands;

namespace Moongate.Tests.Server.Core.Commands;

public sealed class CommandRegistryTests
{
    [Fact]
    public void Register_AliasesShareOneRegistrationAndOneDefinition()
    {
        using var container = new Container();

        container.RegisterCommand<RecordingCommandExecutor>("echo|e|say");

        var registrations = container.Resolve<CommandRegistry>().Registrations;
        Assert.Equal(3, registrations.Count);
        Assert.Same(registrations["echo"], registrations["e"]);
        Assert.Same(registrations["echo"], registrations["say"]);
        Assert.Equal("echo", registrations["say"].Definition.Name);
        Assert.Equal(new[] { "echo", "e", "say" }, registrations["echo"].Definition.Aliases);
    }

    [Fact]
    public void Register_NormalizesAliasesAndLooksThemUpCaseInsensitively()
    {
        using var container = new Container();

        container.RegisterCommand<RecordingCommandExecutor>(" Echo | E ");

        var registrations = container.Resolve<CommandRegistry>().Registrations;
        Assert.Equal("echo", registrations["ECHO"].Definition.Name);
        Assert.Equal(new[] { "echo", "e" }, registrations["echo"].Definition.Aliases);
    }

    [Fact]
    public void Register_DuplicateAliasRejectsBeforeContainerMutation()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo|e");

        Assert.Throws<InvalidOperationException>(() => container.RegisterCommand<ThrowingCommandExecutor>("boom|E"));

        Assert.Equal(2, container.Resolve<CommandRegistry>().Registrations.Count);
        Assert.False(container.IsRegistered<ThrowingCommandExecutor>());
    }

    [Fact]
    public void Register_RepeatedAliasWithinOneNameIsRejected()
    {
        using var container = new Container();

        Assert.Throws<InvalidOperationException>(() => container.RegisterCommand<RecordingCommandExecutor>("echo|echo"));

        Assert.Empty(container.Resolve<CommandRegistry>().Registrations);
    }

    [Fact]
    public void Register_BlankNameIsRejected()
    {
        using var container = new Container();

        Assert.Throws<ArgumentException>(() => container.RegisterCommand<RecordingCommandExecutor>("   "));

        Assert.Empty(container.Resolve<CommandRegistry>().Registrations);
    }

    [Fact]
    public void Register_AfterFreezeRejectsBeforeContainerMutation()
    {
        using var container = new Container();
        container.RegisterInstance(new CommandRegistry());
        var frozen = container.Resolve<CommandRegistry>().Freeze();

        Assert.Throws<InvalidOperationException>(() => container.RegisterCommand<RecordingCommandExecutor>("echo"));

        Assert.Empty(frozen);
        Assert.False(container.IsRegistered<RecordingCommandExecutor>());
    }

    [Fact]
    public void Register_PreRegisteredTransientExecutorRejectsWithoutPartialMetadata()
    {
        using var container = new Container();
        container.Register<RecordingCommandExecutor>(Reuse.Transient);

        Assert.Throws<InvalidOperationException>(() => container.RegisterCommand<RecordingCommandExecutor>("echo"));

        Assert.Empty(container.Resolve<CommandRegistry>().Registrations);
    }

    [Fact]
    public void Freeze_ReturnsTheSameSnapshotOnRepeatedCalls()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo");
        var registry = container.Resolve<CommandRegistry>();

        var first = registry.Freeze();
        var second = registry.Freeze();

        Assert.Same(first, second);
        Assert.Same(first, registry.Registrations);
    }

    [Fact]
    public void Register_CarriesDescriptionSourceAndMinimumAccountType()
    {
        using var container = new Container();

        container.RegisterCommand<RecordingCommandExecutor>(
            "echo",
            "Echoes back its arguments.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.GameMaster
        );

        var definition = container.Resolve<CommandRegistry>().Registrations["echo"].Definition;
        Assert.Equal("Echoes back its arguments.", definition.Description);
        Assert.Equal(CommandSourceType.Console | CommandSourceType.InGame, definition.Source);
        Assert.Equal(AccountType.GameMaster, definition.MinimumAccountType);
        Assert.Equal(typeof(RecordingCommandExecutor), definition.ExecutorType);
    }

    [Fact]
    public async Task Bind_ResolvesTheSingletonExecutorAndInvokesIt()
    {
        using var container = new Container();
        container.RegisterCommand<RecordingCommandExecutor>("echo");
        var registration = container.Resolve<CommandRegistry>().Freeze()["echo"];
        var context = new CommandContext("echo hi", "echo", ["hi"], CommandSourceType.Console, null);

        await registration.Bind(container)(context);

        var executor = container.Resolve<RecordingCommandExecutor>();
        Assert.Same(context, Assert.Single(executor.Invocations));
        Assert.Same(executor, container.Resolve<RecordingCommandExecutor>());
    }
}
