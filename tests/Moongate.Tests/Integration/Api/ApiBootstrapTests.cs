using DryIoc;
using Moongate.Api.Registry;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Extensions;
using Moongate.Tests.Support.Server;
using Moongate.Tests.TestSupport.Api;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Integration.Api;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class ApiBootstrapTests
{
    [Theory, InlineData("before"), InlineData("after"), InlineData("plugin")]
    public async Task StartAsync_HandlerRegistration_UsesSharedRegistryAndServesCalls(string registration)
    {
        using var fixture = new ApiHostFixture();
        using var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None).RegisterServices(services =>
        {
            RegisterInputs(services, fixture);
            if (registration == "before") { services.RegisterApiHandler<IncrementHandler>(); }
            var existing = services.IsRegistered<ApiRegistry>() ? services.Resolve<ApiRegistry>() : null;
            ApiServerRegistration.Register(services);
            if (existing is not null) { Assert.Same(existing, services.Resolve<ApiRegistry>()); }
            if (registration == "after") { services.RegisterApiHandler<IncrementHandler>(); }
            if (registration == "plugin")
            {
                services.RegisterInstance<IPluginLoaderService>(new ApiRegistrationPluginLoader(services));
            }
            return services;
        });
        var service = container.Resolve<IApiServerService>();
        Assert.Same(service, container.Resolve<IApiServerService>());
        Assert.False(container.Resolve<ApiRegistry>().IsFrozen);
        try
        {
            await bootstrap.StartAsync();
            Assert.True(container.Resolve<ApiRegistry>().IsFrozen);
            await using var client = fixture.CreateClient();
            await using var connection = await client.ConnectAsync(service.Endpoint!, "localhost", "server");
            Assert.Equal(42, (await connection.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 })).Value);
        }
        finally { await bootstrap.StopAsync(); }
        Assert.Null(service.Endpoint);
        fixture.AssertPortReleased();
    }

    [Fact]
    public async Task StartAsync_LaterFailure_RollsBackApiBeforeDependencies()
    {
        using var fixture = new ApiHostFixture();
        using var container = new Container();
        IApiServerService? api = null;
        var dependencyStopped = false;
        var failure = new InvalidOperationException("Later startup failure");
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None).RegisterServices(services =>
        {
            RegisterInputs(services, fixture);
            services.RegisterMoongateService(new CallbackStartupService(
                () => Task.CompletedTask,
                () =>
                {
                    Assert.Null(api!.Endpoint);
                    fixture.AssertPortReleased();
                    dependencyStopped = true;
                    return Task.CompletedTask;
                }), 100);
            services.RegisterApiHandler<IncrementHandler>();
            ApiServerRegistration.Register(services);
            // A distinct contract avoids replacing the earlier callback's registration.
            services.RegisterMoongateService<IMoongateStartupService, CallbackStartupService>(
                new CallbackStartupService(() =>
                {
                    Assert.NotNull(api!.Endpoint);
                    throw failure;
                }, () => Task.CompletedTask), 120);
            return services;
        });
        api = container.Resolve<IApiServerService>();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.StartAsync());
        Assert.Same(failure, observed);
        Assert.True(dependencyStopped);
        Assert.Null(api.Endpoint);
        await bootstrap.StopAsync();
    }

    [Fact]
    public async Task StartAsync_MissingCertificate_RollsBackEarlierServices()
    {
        using var fixture = new ApiHostFixture();
        fixture.Config.CertificatePath = "missing.pfx";
        using var container = new Container();
        var stopped = false;
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None).RegisterServices(services =>
        {
            RegisterInputs(services, fixture);
            services.RegisterMoongateService(new CallbackStartupService(
                () => Task.CompletedTask, () => { stopped = true; return Task.CompletedTask; }), 100);
            return ApiServerRegistration.Register(services);
        });
        var api = container.Resolve<IApiServerService>();
        await Assert.ThrowsAnyAsync<Exception>(() => bootstrap.StartAsync());
        Assert.True(stopped);
        Assert.Null(api.Endpoint);
        fixture.AssertPortReleased();
        await bootstrap.StopAsync();
    }

    private static void RegisterInputs(Container container, ApiHostFixture fixture)
    {
        container.RegisterInstance(new MoongateServerConfig { Api = fixture.Config });
        container.RegisterInstance(fixture.Directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
    }
}
