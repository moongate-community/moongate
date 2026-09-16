using System.Runtime.Loader;

using DryIoc;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Plugins;
using Moongate.Server.Services.Plugins;
using Moongate.Tests.TestSupport.Plugins;

namespace Moongate.Tests.Integration.Plugins;

public sealed class PluginLoaderServiceTests
{
    [Fact]
    public void LoadPlugins_EmptyDirectory_CanBeCalledRepeatedly()
    {
        using var files = new PluginDirectoryFixture();
        using var container = new Container();
        using var loader = new PluginLoaderService(container, files.Directories);

        loader.LoadPlugins();
        loader.LoadPlugins();

        Assert.Empty(loader.Plugins);
        Assert.Empty(container.Resolve<MoongatePluginRegistry>().Plugins);
    }

    [Fact]
    public void LoadPlugins_DiskPlugins_ResolveDependenciesAndRegisterInDependencyOrder()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("DependentPlugin");
        files.Deploy("FoundationPlugin");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        using var loader = new PluginLoaderService(container, files.Directories);

        loader.LoadPlugins();
        loader.LoadPlugins();

        Assert.Equal(new[] { "loader.foundation", "loader.dependent" }, loader.Plugins.Select(plugin => plugin.Id));
        Assert.Equal(new[] { "foundation:register", "dependent:register:private dependency loaded" }, events);
        var service = container.Resolve<IMoongateStartupService>();
        var context = AssemblyLoadContext.GetLoadContext(service.GetType().Assembly);
        Assert.NotNull(context);
        Assert.NotSame(AssemblyLoadContext.Default, context);
        Assert.True(context.IsCollectible);
        Assert.Contains(context.Assemblies, assembly => assembly.GetName().Name == "PrivateDependency");
        Assert.DoesNotContain(context.Assemblies, assembly => assembly.GetName().Name == "Moongate.Server.Core");
    }

    [Fact]
    public void LoadPlugins_DiskPluginCanDependOnInternalPlugin()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("DependentPlugin");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        container.RegisterMoongatePlugin(new RecordingPlugin(new MoongatePluginData(
            "loader.foundation", "Internal foundation", new Version(1, 0))));
        var registry = container.Resolve<MoongatePluginRegistry>();
        using var loader = new PluginLoaderService(container, files.Directories);

        loader.LoadPlugins();

        Assert.Same(registry, container.Resolve<MoongatePluginRegistry>());
        Assert.Equal(new[] { "loader.foundation", "loader.dependent" }, loader.Plugins.Select(plugin => plugin.Id));
        Assert.Equal(new[] { "dependent:register:private dependency loaded" }, events);
    }

    [Fact]
    public void LoadPlugins_ConcurrentCalls_RegisterEachPluginOnce()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("FoundationPlugin");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        using var loader = new PluginLoaderService(container, files.Directories);

        Parallel.For(0, 8, _ => loader.LoadPlugins());

        Assert.Equal(new[] { "foundation:register" }, events);
        Assert.Single(loader.Plugins);
    }

    [Fact]
    public void LoadPlugins_DuplicateDiskIds_RejectsBeforeRegister()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("FoundationPlugin");
        files.Deploy("FoundationPlugin", "FoundationPluginCopy");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        using var loader = new PluginLoaderService(container, files.Directories);

        Assert.Throws<InvalidOperationException>(loader.LoadPlugins);
        Assert.Empty(loader.Plugins);
        Assert.Empty(events);
    }

    [Fact]
    public void LoadPlugins_MissingPrivateDependency_ReportsCauseAndRejectsRetry()
    {
        using var files = new PluginDirectoryFixture();
        var directory = files.Deploy("DependentPlugin");
        files.Deploy("FoundationPlugin");
        File.Delete(Path.Combine(directory, "PrivateDependency.dll"));
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        using var loader = new PluginLoaderService(container, files.Directories);

        var error = Assert.Throws<InvalidOperationException>(loader.LoadPlugins);

        Assert.Contains("PrivateDependency", error.ToString());
        Assert.Throws<InvalidOperationException>(loader.LoadPlugins);
        Assert.Equal(new[] { "foundation:register" }, events);
    }

    [Fact]
    public void LoadPlugins_AssemblyWithoutPluginTypes_RejectsBundle()
    {
        using var files = new PluginDirectoryFixture();
        var directory = files.Deploy("DependentPlugin");
        var path = Path.Combine(directory, "DependentPlugin.dll");
        File.Copy(Path.Combine(directory, "PrivateDependency.dll"), path, true);
        using var container = new Container();
        using var loader = new PluginLoaderService(container, files.Directories);

        var error = Assert.Throws<InvalidOperationException>(loader.LoadPlugins);

        Assert.Contains("IMoongatePlugin", error.ToString());
        Assert.Contains(path, error.ToString());
        Assert.Empty(loader.Plugins);
    }

    [Fact]
    public void LoadPlugins_DuplicateInternalAndDiskId_RejectsBeforeDiskRegistration()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("FoundationPlugin");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        container.RegisterMoongatePlugin(new RecordingPlugin(new MoongatePluginData(
            "loader.foundation", "Internal foundation", new Version(1, 0))));
        using var loader = new PluginLoaderService(container, files.Directories);

        Assert.Throws<InvalidOperationException>(loader.LoadPlugins);
        Assert.Single(loader.Plugins);
        Assert.Empty(events);
    }

    [Fact]
    public void LoadPlugins_MissingDependency_AbortsBeforeRegister()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("DependentPlugin");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        using var loader = new PluginLoaderService(container, files.Directories);

        var error = Assert.Throws<InvalidOperationException>(loader.LoadPlugins);

        Assert.Contains("loader.foundation", error.ToString());
        Assert.Empty(loader.Plugins);
        Assert.Empty(events);
    }

    [Fact]
    public void LoadPlugins_RegistrationFailure_RejectsRetry()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("FailingPlugin");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        using var loader = new PluginLoaderService(container, files.Directories);

        var error = Assert.Throws<InvalidOperationException>(loader.LoadPlugins);
        Assert.Contains("Fixture registration failed.", error.ToString());
        Assert.Throws<InvalidOperationException>(loader.LoadPlugins);
        Assert.Equal(new[] { "failing:register" }, events);
    }

    [Fact]
    public void LoadPlugins_MissingEntryAssembly_ReportsBundlePath()
    {
        using var files = new PluginDirectoryFixture();
        var directory = Directory.CreateDirectory(Path.Combine(files.Directories["plugins"], "Missing"));
        using var container = new Container();
        using var loader = new PluginLoaderService(container, files.Directories);

        var error = Assert.Throws<InvalidOperationException>(loader.LoadPlugins);

        Assert.Contains(Path.Combine(directory.FullName, "Missing.dll"), error.ToString());
        Assert.Empty(loader.Plugins);
    }

    [Fact]
    public void LoadPlugins_InvalidAssembly_ReportsPathAndCause()
    {
        using var files = new PluginDirectoryFixture();
        var directory = Directory.CreateDirectory(Path.Combine(files.Directories["plugins"], "Broken"));
        var path = Path.Combine(directory.FullName, "Broken.dll");
        File.WriteAllText(path, "not an assembly");
        using var container = new Container();
        using var loader = new PluginLoaderService(container, files.Directories);

        var error = Assert.Throws<InvalidOperationException>(loader.LoadPlugins);

        Assert.Contains(path, error.ToString());
        Assert.Empty(loader.Plugins);
    }

    [Fact]
    public void Dispose_LoadedContexts_UnloadsOnceAndClosesLoader()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("DependentPlugin");
        files.Deploy("FoundationPlugin");
        using var container = new Container();
        container.RegisterInstance(new List<string>());
        var loader = new PluginLoaderService(container, files.Directories);
        loader.LoadPlugins();
        var service = container.Resolve<IMoongateStartupService>();
        var context = AssemblyLoadContext.GetLoadContext(service.GetType().Assembly)!;
        var unloading = 0;
        context.Unloading += _ => unloading++;

        loader.Dispose();
        loader.Dispose();

        Assert.Equal(1, unloading);
        Assert.Throws<ObjectDisposedException>(loader.LoadPlugins);
    }

    [Fact]
    public async Task Bootstrap_LoadsPluginsBeforeStartingTheirServicesAndPublishingEvents()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("DependentPlugin");
        files.Deploy("FoundationPlugin");
        using var container = new Container();
        List<string> events = [];
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None)
            .RegisterServices(services =>
            {
                services.RegisterInstance(events);
                services.RegisterInstance(files.Directories);
                return services.RegisterMoongateService<IPluginLoaderService, PluginLoaderService>(
                    () => new PluginLoaderService(services, files.Directories));
            });

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(new[]
        {
            "foundation:register", "dependent:register:private dependency loaded", "dependent:start",
            "dependent:started", "dependent:stopping", "dependent:stop", "dependent:stopped", "dependent:dispose"
        }, events);
    }

    [Fact]
    public async Task Bootstrap_PluginLoadFailure_DisposesContainerWithoutStartingServices()
    {
        using var files = new PluginDirectoryFixture();
        files.Deploy("FailingPlugin");
        using var container = new Container();
        List<string> events = [];
        container.RegisterInstance(events);
        container.RegisterInstance(files.Directories);
        container.RegisterMoongateService<IPluginLoaderService, PluginLoaderService>(
            () => new PluginLoaderService(container, files.Directories));
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(bootstrap.StartAsync);
        await bootstrap.StopAsync();

        Assert.True(container.IsDisposed);
        Assert.Equal(new[] { "failing:register", "failing:stopping:private dependency loaded" }, events);
    }
}
