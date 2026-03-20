using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Plugin.Abstractions.Interfaces;
using Moongate.Server.Data.Plugins;
using Moongate.Server.Json;
using Moongate.Server.Services.Plugins;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Services.Plugins;

public class PluginLoaderServiceTests
{
    [Test]
    public void LoadPlugins_WhenManifestReferencesValidPluginType_ShouldInstantiatePlugin()
    {
        using var temp = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var pluginDirectory = Path.Combine(directoriesConfig[DirectoryType.Plugins], "test-plugin");
        Directory.CreateDirectory(pluginDirectory);
        WriteManifest(
            Path.Combine(pluginDirectory, "manifest.json"),
            new MoongatePluginManifest
            {
                Id = "test-plugin",
                Name = "Test Plugin",
                Version = "1.0.0",
                Authors = ["Squid"],
                EntryAssembly = typeof(TestPlugin).Assembly.Location,
                EntryType = typeof(TestPlugin).FullName
            }
        );

        var loader = new PluginLoaderService(
            new PluginDiscoveryService(directoriesConfig),
            new PluginDependencyGraphService()
        );

        var loadedPlugins = loader.LoadPlugins();

        Assert.That(loadedPlugins, Has.Count.EqualTo(1));
        Assert.That(loadedPlugins[0].Instance, Is.TypeOf<TestPlugin>());
        Assert.That(loadedPlugins[0].DiscoveredPlugin.PluginId, Is.EqualTo("test-plugin"));
    }

    [Test]
    public void LoadPlugins_WhenEntryTypeIsNotPlugin_ShouldThrow()
    {
        using var temp = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var pluginDirectory = Path.Combine(directoriesConfig[DirectoryType.Plugins], "bad-plugin");
        Directory.CreateDirectory(pluginDirectory);
        WriteManifest(
            Path.Combine(pluginDirectory, "manifest.json"),
            new MoongatePluginManifest
            {
                Id = "bad-plugin",
                Name = "Bad Plugin",
                Version = "1.0.0",
                Authors = ["Squid"],
                EntryAssembly = typeof(NotAPluginType).Assembly.Location,
                EntryType = typeof(NotAPluginType).FullName
            }
        );

        var loader = new PluginLoaderService(
            new PluginDiscoveryService(directoriesConfig),
            new PluginDependencyGraphService()
        );

        var ex = Assert.Throws<InvalidOperationException>(() => loader.LoadPlugins());

        Assert.That(ex!.Message, Does.Contain("does not implement IMoongatePlugin"));
    }

    private static void WriteManifest(string path, MoongatePluginManifest manifest)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(manifest, MoongatePluginJsonContext.Default.MoongatePluginManifest)
        );
    }

    private sealed class TestPlugin : IMoongatePlugin
    {
        public string Id => "test-plugin";

        public string Name => "Test Plugin";

        public string Version => "1.0.0";

        public IReadOnlyList<string> Authors => ["Squid"];

        public string? Description => "Test";

        public void Configure(IMoongatePluginContext context)
            => _ = context;

        public Task InitializeAsync(IMoongatePluginRuntimeContext context, CancellationToken cancellationToken)
        {
            _ = context;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class NotAPluginType { }
}
