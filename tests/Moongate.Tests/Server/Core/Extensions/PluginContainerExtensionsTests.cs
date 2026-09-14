using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Plugins;
using Moongate.Tests.TestSupport.Plugins;
using Moongate.Tests.TestSupport.Services;
using Moongate.Tests.TestSupport.Services.Interfaces;

namespace Moongate.Tests.Server.Core.Extensions;

public sealed class PluginContainerExtensionsTests
{
    [Fact]
    public void GenericOverload_RegistersPluginAndServices()
    {
        using var container = new Container();

        var result = container.RegisterMoongatePlugin<DefaultRegistrationPlugin>();

        Assert.Same(container, result);
        Assert.Equal("default", Assert.Single(container.Resolve<MoongatePluginRegistry>().Plugins).Id);
        Assert.IsType<RegistrationService>(container.Resolve<IRegistrationService>());
    }

    [Fact]
    public void InstanceOverload_UsesProvidedInstanceAndReusesRegistry()
    {
        using var container = new Container();
        var instance = new RecordingPlugin(new MoongatePluginData("configured", "Configured", new Version(1, 0, 0)));
        container.RegisterMoongatePlugin<DefaultRegistrationPlugin>();
        var registry = container.Resolve<MoongatePluginRegistry>();

        var result = container.RegisterMoongatePlugin(instance);

        Assert.Same(container, result);
        Assert.Same(registry, container.Resolve<MoongatePluginRegistry>());
        Assert.Equal(1, instance.RegisterCalls);
        Assert.Equal(2, registry.Plugins.Count);
    }

    [Fact]
    public void BatchOverload_OrdersDependenciesAndSupportsChaining()
    {
        using var container = new Container();
        var root = new RecordingPlugin(new MoongatePluginData("root", "Root", new Version(1, 0, 0)));
        var app = new RecordingPlugin(new MoongatePluginData("app", "App", new Version(1, 0, 0),
            dependencies: [new MoongatePluginDependencyData("root")]));

        var result = container.RegisterMoongatePlugins(app, root)
            .RegisterMoongatePlugin<DefaultRegistrationPlugin>();

        Assert.Same(container, result);
        Assert.Equal(new[] { "root", "app", "default" },
            container.Resolve<MoongatePluginRegistry>().Plugins.Select(plugin => plugin.Id));
    }

    [Fact]
    public void Overloads_ShareDuplicateDetection()
    {
        using var container = new Container();
        container.RegisterMoongatePlugin<DefaultRegistrationPlugin>();
        var duplicate = new RecordingPlugin(new MoongatePluginData("DEFAULT", "Duplicate", new Version(2, 0, 0)));

        Assert.Throws<InvalidOperationException>(() => container.RegisterMoongatePlugins(duplicate));
        Assert.Equal(0, duplicate.RegisterCalls);
        Assert.Single(container.Resolve<MoongatePluginRegistry>().Plugins);
    }

    [Fact]
    public void Overloads_RejectNullContainerOrInput()
    {
        using var container = new Container();
        Container missing = null!;
        var instance = new DefaultRegistrationPlugin();

        Assert.Throws<ArgumentNullException>(() => missing.RegisterMoongatePlugin<DefaultRegistrationPlugin>());
        Assert.Throws<ArgumentNullException>(() => missing.RegisterMoongatePlugin(instance));
        Assert.Throws<ArgumentNullException>(() => missing.RegisterMoongatePlugins(instance));
        Assert.Throws<ArgumentNullException>(() => container.RegisterMoongatePlugin((IMoongatePlugin)null!));
        Assert.Throws<ArgumentNullException>(() => container.RegisterMoongatePlugins((IMoongatePlugin[])null!));
    }
}
