using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Plugins;
using Moongate.Tests.TestSupport.Plugins;
using Moongate.Tests.TestSupport.Services;
using Moongate.Tests.TestSupport.Services.Interfaces;

namespace Moongate.Tests.Server.Core.Plugins;

public sealed class MoongatePluginRegistryTests
{
    [Fact]
    public void Plugins_IsALiveReadOnlyViewInRegistrationOrder()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var view = registry.Plugins;
        registry.Register([Create("second"), Create("first")]);
        Assert.Equal(new[] { "second", "first" }, view.Select(plugin => plugin.Id));
        var list = Assert.IsAssignableFrom<IList<MoongatePluginData>>(view);
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Equal(2, registry.Plugins.Count);
    }

    [Theory, InlineData("1.0.0"), InlineData("1.1.0")]
    public void Register_AcceptsInclusiveMinimumVersionFromExistingPlugin(string version)
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var core = Create("core", version: Version.Parse(version));
        registry.Register(core);
        var app = Create("app", dependencies: [new("CORE", new(1, 0, 0))]);
        registry.Register(app);
        Assert.Equal(1, core.RegisterCalls);
        Assert.Equal(1, app.RegisterCalls);
        Assert.Equal(2, registry.Plugins.Count);
    }

    [Fact]
    public void Register_CallbackFailurePreservesSuccessfulPrefixAndFaultsRegistry()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var cause = new ApplicationException("Registration failed.");
        var first = Create("first");
        var broken = Create(
            "broken",
            current =>
            {
                current.RegisterInstance(new RegistrationDependency());

                throw cause;
            }
        );
        var last = Create("last");
        var error = Assert.Throws<InvalidOperationException>(() => registry.Register([first, broken, last]));
        Assert.Contains("broken", error.Message);
        Assert.Same(cause, error.InnerException);
        Assert.Equal("first", Assert.Single(registry.Plugins).Id);
        Assert.True(container.IsRegistered<RegistrationDependency>());
        Assert.Equal(0, last.RegisterCalls);
        Assert.Throws<InvalidOperationException>(() => registry.Register(last));
        Assert.Equal(0, last.RegisterCalls);
    }

    [Fact]
    public void Register_DependencyWithoutMinimumAcceptsAnyAvailableVersion()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var core = Create("core", version: new(0, 0, 1));
        var app = Create("app", dependencies: [new("core")]);
        registry.Register([app, core]);
        Assert.Equal(new[] { "core", "app" }, registry.Plugins.Select(plugin => plugin.Id));
    }

    [Fact]
    public void Register_EmptyBatchDoesNothing()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        registry.Register(Array.Empty<IMoongatePlugin>());
        Assert.Empty(registry.Plugins);
        registry.Register(Create("valid"));
        Assert.Single(registry.Plugins);
    }

    [Fact]
    public void Register_ExecutesSharedDependencyOnlyOnce()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var shared = Create("shared");
        var left = Create("left", dependencies: [new("shared")]);
        var right = Create("right", dependencies: [new("shared")]);
        var app = Create(
            "app",
            dependencies: [new("left"), new("right")]
        );

        registry.Register([app, right, shared, left]);

        Assert.Equal(new[] { "shared", "left", "right", "app" }, registry.Plugins.Select(plugin => plugin.Id));
        Assert.Equal(1, shared.RegisterCalls);
    }

    [Fact]
    public void Register_MissingDependencyRejectsEntireBatchAndAllowsCorrection()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var valid = Create("valid");
        var invalid = Create("app", dependencies: [new("missing")]);
        var error = Assert.Throws<InvalidOperationException>(() => registry.Register([valid, invalid]));
        Assert.Contains("app", error.Message);
        Assert.Contains("missing", error.Message);
        Assert.Equal(0, valid.RegisterCalls);
        Assert.Equal(0, invalid.RegisterCalls);
        Assert.Empty(registry.Plugins);
        registry.Register(valid);
        Assert.Single(registry.Plugins);
    }

    [Fact]
    public void Register_OrdersTransitiveDependenciesAndReadsMetadataOnce()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var calls = new List<string>();
        var root = Create("root", _ => calls.Add("root"));
        var middle = Create("middle", _ => calls.Add("middle"), [new("ROOT")]);
        var leaf = Create("leaf", _ => calls.Add("leaf"), [new("middle")]);
        var other = Create("other", _ => calls.Add("other"));

        registry.Register([leaf, other, middle, root]);

        Assert.Equal(new[] { "root", "middle", "leaf", "other" }, calls);
        Assert.Equal(calls, registry.Plugins.Select(plugin => plugin.Id));
        Assert.All(
            new[] { root, middle, leaf, other },
            plugin =>
            {
                Assert.Equal(1, plugin.RegisterCalls);
                Assert.Equal(1, plugin.MetadataReads);
            }
        );
    }

    [Fact]
    public void Register_PreservesLazySingletonServiceAndRegistrationMetadata()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var dependency = new RegistrationDependency();
        container.RegisterInstance(dependency);
        var factoryCalls = 0;
        var plugin = Create(
            "services",
            current => current.RegisterMoongateService<IRegistrationService, StartupRegistrationService>(
                resolver =>
                {
                    factoryCalls++;

                    return new(resolver.Resolve<RegistrationDependency>());
                },
                42
            )
        );
        registry.Register(plugin);
        Assert.Equal(0, factoryCalls);
        var metadata = Assert.Single(container.Resolve<List<ServiceRegistrationData>>());
        Assert.Equal(typeof(IRegistrationService), metadata.ServiceType);
        Assert.Equal(typeof(StartupRegistrationService), metadata.ImplementationType);
        Assert.True(metadata.IsAutostart);
        Assert.Equal(42, metadata.Priority);
        var service = container.Resolve<IRegistrationService>();
        Assert.Same(dependency, service.Dependency);
        Assert.Same(service, container.Resolve<IRegistrationService>());
        Assert.Equal(1, factoryCalls);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Register_RejectsCyclesBeforeAnyCallback(bool selfDependency)
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var independent = Create("independent");
        var first = Create("first", dependencies: [new(selfDependency ? "first" : "second")]);
        var second = Create("second", dependencies: [new("first")]);
        var error = Assert.Throws<InvalidOperationException>(() => registry.Register([independent, first, second]));
        Assert.Contains("first", error.Message);
        Assert.Contains("->", error.Message);
        Assert.All(new[] { independent, first, second }, plugin => Assert.Equal(0, plugin.RegisterCalls));
        Assert.Empty(registry.Plugins);
    }

    [Fact]
    public void Register_RejectsDuplicateAlreadyRegisteredId()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var original = Create("core");
        var duplicate = Create("CORE");
        registry.Register(original);
        Assert.Throws<InvalidOperationException>(() => registry.Register(duplicate));
        Assert.Equal(1, original.RegisterCalls);
        Assert.Equal(0, duplicate.RegisterCalls);
        Assert.Single(registry.Plugins);
    }

    [Theory, InlineData("core"), InlineData("CORE")]
    public void Register_RejectsDuplicateIdsInBatchRegardlessOfVersion(string duplicateId)
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var first = Create("core");
        var second = Create(duplicateId, version: new(2, 0, 0));
        Assert.Throws<InvalidOperationException>(() => registry.Register([first, second]));
        Assert.Equal(0, first.RegisterCalls);
        Assert.Equal(0, second.RegisterCalls);
        Assert.Empty(registry.Plugins);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Register_RejectsInsufficientVersionBeforeNewCallbacks(bool alreadyRegistered)
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var core = Create("core", version: new(1, 0, 0));
        var app = Create("app", dependencies: [new("core", new(2, 0, 0))]);

        if (alreadyRegistered)
        {
            registry.Register(core);
        }

        IMoongatePlugin[] batch = alreadyRegistered ? [app] : [core, app];
        var error = Assert.Throws<InvalidOperationException>(() => registry.Register(batch));
        Assert.Contains("app", error.Message);
        Assert.Contains("core", error.Message);
        Assert.Contains("2.0.0", error.Message);
        Assert.Contains("1.0.0", error.Message);
        Assert.Equal(0, app.RegisterCalls);
        Assert.Equal(alreadyRegistered ? 1 : 0, core.RegisterCalls);
    }

    [Fact]
    public void Register_RejectsNestedRegistration()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var nested = Create("nested");
        var outer = Create("outer", _ => registry.Register(nested));
        var error = Assert.Throws<InvalidOperationException>(() => registry.Register(outer));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(0, nested.RegisterCalls);
        Assert.Empty(registry.Plugins);
        Assert.Throws<InvalidOperationException>(() => registry.Register(nested));
    }

    [Fact]
    public void Register_RejectsNullInputsWithoutPoisoningRegistry()
    {
        using var container = new Container();
        var registry = new MoongatePluginRegistry(container);
        var valid = Create("valid");
        var invalid = new RecordingPlugin(null!);
        Assert.Throws<ArgumentNullException>(() => registry.Register((IMoongatePlugin)null!));
        Assert.Throws<ArgumentNullException>(() => registry.Register((IEnumerable<IMoongatePlugin>)null!));
        Assert.Throws<ArgumentNullException>(() => registry.Register([valid, null!]));
        Assert.Throws<InvalidOperationException>(() => registry.Register([valid, invalid]));
        Assert.Equal(0, valid.RegisterCalls);
        Assert.Equal(0, invalid.RegisterCalls);
        registry.Register(valid);
        Assert.Single(registry.Plugins);
    }

    private static RecordingPlugin Create(
        string id,
        Action<Container>? register = null,
        IEnumerable<MoongatePluginDependencyData>? dependencies = null,
        Version? version = null
    )
        => new(
            new(id, id, version ?? new Version(1, 0, 0), dependencies: dependencies),
            register
        );
}
