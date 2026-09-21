using DryIoc;
using Moongate.Api.Registry;
using Moongate.Server.Extensions;
using Moongate.Tests.TestSupport.Api;

namespace Moongate.Tests.Server.Extensions;

public class ApiContainerExtensionsTests
{
    [Fact]
    public void DuplicateOperation_LeavesOriginalHandlerUnchanged()
    {
        using var container = new Container();
        container.RegisterApiHandler<IncrementHandler>();
        Assert.Throws<InvalidOperationException>(() => container.RegisterApiHandler<FailingHandler>());
        Assert.False(container.IsRegistered<FailingHandler>());
        Assert.Equal(1, container.Resolve<ApiRegistry>().HandlerCount);
        container.Resolve<ApiRegistry>().Freeze();
    }

    [Fact]
    public void HandlerConstructorFailure_IsReportedDuringFreeze()
    {
        using var container = new Container();
        container.RegisterApiHandler<FailingHandler>();
        var error = Assert.ThrowsAny<Exception>(() => container.Resolve<ApiRegistry>().Freeze());
        Assert.Contains("Constructor failed.", error.ToString());
        Assert.True(container.Resolve<ApiRegistry>().IsFrozen);
    }

    [Fact]
    public void InvalidOrConflictingRegistration_DoesNotMutateContainer()
    {
        using var container = new Container();
        Assert.Throws<InvalidOperationException>(() => container.RegisterApiHandler<string>());
        Assert.False(container.IsRegistered<ApiRegistry>());
        Assert.False(container.IsRegistered<string>());
        container.Register<IncrementHandler>(Reuse.Transient);
        Assert.Throws<InvalidOperationException>(() => container.RegisterApiHandler<IncrementHandler>());
        Assert.False(container.IsRegistered<ApiRegistry>());
        Assert.NotSame(container.Resolve<IncrementHandler>(), container.Resolve<IncrementHandler>());
    }

    [Fact]
    public void RegisterApiHandler_InfersContractAndOwnsOneSingletonPerContainer()
    {
        using var first = new Container();
        using var second = new Container();
        Assert.Same(first, first.RegisterApiHandler<IncrementHandler>());
        second.RegisterApiHandler<IncrementHandler>();
        var registry = first.Resolve<ApiRegistry>();
        Assert.Equal(1, registry.ContractCount);
        Assert.Equal(1, registry.HandlerCount);
        registry.Freeze();
        Assert.Same(first.Resolve<IncrementHandler>(), first.Resolve<IncrementHandler>());
        Assert.NotSame(first.Resolve<IncrementHandler>(), second.Resolve<IncrementHandler>());
        Assert.NotSame(registry, second.Resolve<ApiRegistry>());
        Assert.Throws<InvalidOperationException>(() => first.RegisterApiHandler<IncrementHandler>());
    }
}
