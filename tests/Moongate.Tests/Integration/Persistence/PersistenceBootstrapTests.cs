using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.Support.Server;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Plugins;
using Moongate.Server.Core.Data.Plugins;

namespace Moongate.Tests.Integration.Persistence;

public sealed class PersistenceBootstrapTests
{
    [Fact]
    public async Task StartAsync_NegativePrioritySentinel_SeesSchemaAfterPluginRegistration()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        var started = false;
        fixture.Container.RegisterInstance<IPluginLoaderService>(new PersistencePluginLoader(fixture.Container));
        fixture.Container.RegisterMoongateService(new CallbackStartupService(async () =>
        {
            Assert.Empty(await fixture.Container.Resolve<IDataAccess<TestEntity>>().GetAllAsync());
            started = true;
        }, () => Task.CompletedTask), -2000);
        var owner = fixture.Owner;
        var bootstrap = new MoongateServerBootstrap(fixture.Container, CancellationToken.None);
        await bootstrap.StartAsync();
        Assert.True(started);
        await bootstrap.StopAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.SaveAllAsync());
    }

    [Fact]
    public async Task StartAsync_SchemaMissing_StartsNoServicesAndDisposesOwner()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync(autoSync: false);
        fixture.RegisterEntity();
        var started = false;
        fixture.Container.RegisterMoongateService(new CallbackStartupService(() => { started = true; return Task.CompletedTask; }, () => Task.CompletedTask), -2000);
        var owner = fixture.Owner;
        var bootstrap = new MoongateServerBootstrap(fixture.Container, CancellationToken.None);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(bootstrap.StartAsync);
        Assert.Contains("schema", error.Message);
        Assert.False(started);
        Assert.True(fixture.Container.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.SaveAllAsync());
        await bootstrap.StopAsync();
    }

    [Fact]
    public async Task StartAndStopAsync_NoEntities_NeverResolvesEnvironmentFactories()
    {
        var container = new Container();
        container.RegisterMoongatePersistence(new PostgreSqlPersistenceOptions());
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync();
        await bootstrap.StopAsync();
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task StartAsync_ServiceFails_RetainsFailureAndDisposesInitializedOwner()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        fixture.RegisterEntity();
        var failure = new IOException("startup failed");
        var stopped = false;
        fixture.Container.RegisterMoongateService(new CallbackStartupService(() => throw failure, () => { stopped = true; return Task.CompletedTask; }));
        var owner = fixture.Owner;
        var bootstrap = new MoongateServerBootstrap(fixture.Container, CancellationToken.None);
        Assert.Same(failure, await Record.ExceptionAsync(bootstrap.StartAsync));
        Assert.True(stopped);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.SaveAllAsync());
    }
    [Fact]
    public async Task StopAsync_ServiceFails_DisposesOwnerAndRetainsFailureOnRepeatedStop()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        fixture.RegisterEntity();
        var failure = new IOException("cleanup failed");
        fixture.Container.RegisterMoongateService(new CallbackStartupService(() => Task.CompletedTask, () => throw failure));
        var owner = fixture.Owner;
        var bootstrap = new MoongateServerBootstrap(fixture.Container, CancellationToken.None);
        await bootstrap.StartAsync();
        Assert.Same(failure, await Record.ExceptionAsync(bootstrap.StopAsync));
        Assert.Same(failure, await Record.ExceptionAsync(bootstrap.StopAsync));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.SaveAllAsync());
    }

}
