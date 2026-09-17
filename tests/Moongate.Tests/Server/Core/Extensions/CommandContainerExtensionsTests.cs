using DryIoc;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Tests.TestSupport.Commands;

namespace Moongate.Tests.Server.Core.Extensions;

public sealed class CommandContainerExtensionsTests
{
    [Fact]
    public void RegisterCommand_CreatesTheRegistryOnFirstUseAndReusesIt()
    {
        using var container = new Container();

        container.RegisterCommand<RecordingCommandExecutor>("echo");
        var registry = container.Resolve<CommandRegistry>();
        container.RegisterCommand<ThrowingCommandExecutor>("boom");

        Assert.Same(registry, container.Resolve<CommandRegistry>());
        Assert.Equal(2, registry.Registrations.Count);
    }

    [Fact]
    public void RegisterCommand_RegistersTheExecutorAsASingleton()
    {
        using var container = new Container();

        container.RegisterCommand<RecordingCommandExecutor>("echo");

        Assert.True(container.IsRegistered<RecordingCommandExecutor>());
        Assert.Same(container.Resolve<RecordingCommandExecutor>(), container.Resolve<RecordingCommandExecutor>());
    }

    [Fact]
    public void RegisterCommand_PreservesAnAlreadyRegisteredSingletonInstance()
    {
        using var container = new Container();
        var executor = new RecordingCommandExecutor();
        container.RegisterInstance(executor);

        container.RegisterCommand<RecordingCommandExecutor>("echo");
        container.RegisterCommand<RecordingCommandExecutor>("repeat");

        Assert.Same(executor, container.Resolve<RecordingCommandExecutor>());
        Assert.Equal(2, container.Resolve<CommandRegistry>().Registrations.Count);
    }

    [Fact]
    public void RegisterCommand_ReturnsTheSameContainerForChaining()
    {
        using var container = new Container();

        var returned = container
                       .RegisterCommand<RecordingCommandExecutor>("echo")
                       .RegisterCommand<ThrowingCommandExecutor>("boom");

        Assert.Same(container, returned);
    }
}
