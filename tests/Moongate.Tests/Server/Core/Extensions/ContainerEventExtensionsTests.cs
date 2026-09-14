using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Tests.Support.Events;

namespace Moongate.Tests.Server.Core.Extensions;

public sealed class ContainerEventExtensionsTests
{
    [Fact]
    public async Task OnEvent_Registration_DoesNotInvokeHandlerBeforePublication()
    {
        using var container = new Container();
        var received = 0;

        var result = container.OnEvent<MoongateStartedEvent>((message, token) =>
        {
            received++;
            return Task.CompletedTask;
        });

        Assert.Same(container, result);
        Assert.Equal(0, received);

        await container.Resolve<IMoongateEventBus>().PublishAsync(new MoongateStartedEvent());

        Assert.Equal(1, received);
    }

    [Fact]
    public void RegisterMoongateEventBus_RepeatedRegistration_PreservesSingleton()
    {
        using var container = new Container();

        container.RegisterMoongateEventBus();
        var first = container.Resolve<IMoongateEventBus>();

        var result = container.RegisterMoongateEventBus();

        Assert.Same(container, result);
        Assert.Same(first, container.Resolve<IMoongateEventBus>());
    }

    [Fact]
    public void RegisterMoongateEventBus_ExistingRegistration_PreservesExistingBus()
    {
        using var container = new Container();
        var existing = new RecordingEventBus();
        container.RegisterInstance<IMoongateEventBus>(existing);

        container.RegisterMoongateEventBus();

        Assert.Same(existing, container.Resolve<IMoongateEventBus>());
    }

    [Fact]
    public void OnEvent_Registration_DoesNotResolveUnrelatedServices()
    {
        using var container = new Container();
        var unrelatedResolved = false;
        container.RegisterDelegate(() =>
        {
            unrelatedResolved = true;
            return new object();
        }, Reuse.Singleton);

        container.OnEvent<MoongateStartedEvent>((_, _) => Task.CompletedTask);

        Assert.False(unrelatedResolved);
    }

    [Fact]
    public void Extensions_NullArguments_Throw()
    {
        using var container = new Container();
        Container missing = null!;

        Assert.Throws<ArgumentNullException>(() => missing.RegisterMoongateEventBus());
        Assert.Throws<ArgumentNullException>(() => missing.OnEvent<MoongateStartedEvent>((_, _) => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => container.OnEvent<MoongateStartedEvent>(null!));
    }
}
