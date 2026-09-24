using DryIoc;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Tests.TestSupport.Diagnostics;

namespace Moongate.Tests.Server.Core.Extensions;

public sealed class DiagnosticContainerExtensionsTests
{
    [Fact]
    public void AddMetricProvider_AppendsEveryProviderToTheResolvedCollection()
    {
        using var container = CreateContainer();

        container.AddMetricProvider<TimerMetricsProvider>()
                 .AddMetricProvider<SessionMetricsProvider>();

        var providers = container.Resolve<IEnumerable<IMetricProvider>>().ToArray();

        Assert.Equal(["sessions", "timers"], providers.Select(provider => provider.ProviderName).Order());
    }

    [Fact]
    public void AddMetricProvider_RegistersTheProviderAsASingleton()
    {
        using var container = CreateContainer();

        container.AddMetricProvider<TimerMetricsProvider>();

        Assert.Same(container.Resolve<IMetricProvider>(), container.Resolve<IMetricProvider>());
    }

    [Fact]
    public void AddMetricProvider_ReturnsTheSameContainerForChaining()
    {
        using var container = CreateContainer();

        Assert.Same(container, container.AddMetricProvider<TimerMetricsProvider>());
    }

    private static Container CreateContainer()
    {
        var container = new Container();

        container.RegisterInstance<ITimerService>(new TimerMetricsSourceStub(new()));
        container.RegisterInstance<ISessionService>(new SessionCountSourceStub(0));

        return container;
    }
}
