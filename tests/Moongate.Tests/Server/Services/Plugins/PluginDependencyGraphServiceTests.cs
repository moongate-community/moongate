using Moongate.Server.Data.Internal.Plugins;
using Moongate.Server.Data.Plugins;
using Moongate.Server.Services.Plugins;

namespace Moongate.Tests.Server.Services.Plugins;

public class PluginDependencyGraphServiceTests
{
    [Test]
    public void ResolveDependencyOrder_WhenPluginsHaveLinearDependencies_ShouldReturnTopologicalOrder()
    {
        var service = new PluginDependencyGraphService();
        var plugins = new[]
        {
            CreatePlugin("alpha", dependencies: [CreateDependency("beta")]),
            CreatePlugin("beta", dependencies: [CreateDependency("gamma")]),
            CreatePlugin("gamma")
        };

        var ordered = service.ResolveDependencyOrder(plugins);

        Assert.That(ordered.Select(plugin => plugin.PluginId), Is.EqualTo(["gamma", "beta", "alpha"]));
    }

    [Test]
    public void ResolveDependencyOrder_WhenPluginIdsAreDuplicated_ShouldThrowInvalidOperationException()
    {
        var service = new PluginDependencyGraphService();
        var plugins = new[]
        {
            CreatePlugin("alpha"),
            CreatePlugin("alpha")
        };

        Assert.Throws<InvalidOperationException>(() => service.ResolveDependencyOrder(plugins));
    }

    [Test]
    public void ResolveDependencyOrder_WhenRequiredDependencyIsMissing_ShouldThrowInvalidOperationException()
    {
        var service = new PluginDependencyGraphService();
        var plugins = new[]
        {
            CreatePlugin("alpha", dependencies: [CreateDependency("beta")])
        };

        Assert.Throws<InvalidOperationException>(() => service.ResolveDependencyOrder(plugins));
    }

    [Test]
    public void ResolveDependencyOrder_WhenOptionalDependencyIsMissing_ShouldReturnPlugin()
    {
        var service = new PluginDependencyGraphService();
        var plugins = new[]
        {
            CreatePlugin("alpha", dependencies: [CreateDependency("beta", optional: true)])
        };

        var ordered = service.ResolveDependencyOrder(plugins);

        Assert.That(ordered.Select(plugin => plugin.PluginId), Is.EqualTo(["alpha"]));
    }

    [Test]
    public void ResolveDependencyOrder_WhenCycleExists_ShouldThrowInvalidOperationException()
    {
        var service = new PluginDependencyGraphService();
        var plugins = new[]
        {
            CreatePlugin("alpha", dependencies: [CreateDependency("beta")]),
            CreatePlugin("beta", dependencies: [CreateDependency("alpha")])
        };

        Assert.Throws<InvalidOperationException>(() => service.ResolveDependencyOrder(plugins));
    }

    private static DiscoveredPlugin CreatePlugin(
        string id,
        IReadOnlyList<MoongatePluginDependencyManifest>? dependencies = null
    )
        => new(
            id,
            $"/plugins/{id}",
            $"/plugins/{id}/manifest.json",
            new MoongatePluginManifest
            {
                Id = id,
                Name = id,
                Version = "1.0.0",
                Authors = ["Squid"],
                EntryAssembly = "bin/Plugin.dll",
                EntryType = "Plugin.Entry",
                Dependencies = dependencies ?? []
            }
        );

    private static MoongatePluginDependencyManifest CreateDependency(string id, bool optional = false)
        => new()
        {
            Id = id,
            Optional = optional
        };
}
