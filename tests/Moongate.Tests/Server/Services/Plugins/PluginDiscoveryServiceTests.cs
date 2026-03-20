using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Plugins;
using Moongate.Server.Json;
using Moongate.Server.Services.Plugins;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Services.Plugins;

public class PluginDiscoveryServiceTests
{
    [Test]
    public void DiscoverPlugins_WhenManifestExists_ShouldReturnDiscoveredPlugin()
    {
        using var temp = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var pluginsDirectory = directoriesConfig[DirectoryType.Plugins];
        var pluginDirectory = Path.Combine(pluginsDirectory, "alpha");

        Directory.CreateDirectory(pluginDirectory);
        WriteManifest(
            Path.Combine(pluginDirectory, "manifest.json"),
            new MoongatePluginManifest
            {
                Id = "alpha",
                Name = "Alpha",
                Version = "1.0.0",
                Authors = ["Squid"],
                EntryAssembly = "bin/Alpha.dll",
                EntryType = "Alpha.Plugin"
            }
        );

        var service = new PluginDiscoveryService(directoriesConfig);

        var discoveredPlugins = service.DiscoverPlugins();

        Assert.That(discoveredPlugins, Has.Count.EqualTo(1));
        Assert.Multiple(
            () =>
            {
                Assert.That(discoveredPlugins[0].PluginId, Is.EqualTo("alpha"));
                Assert.That(discoveredPlugins[0].Manifest.EntryAssembly, Is.EqualTo("bin/Alpha.dll"));
                Assert.That(discoveredPlugins[0].Manifest.EntryType, Is.EqualTo("Alpha.Plugin"));
            }
        );
    }

    [Test]
    public void DiscoverPlugins_WhenDirectoryHasNoManifest_ShouldIgnoreDirectory()
    {
        using var temp = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var pluginsDirectory = directoriesConfig[DirectoryType.Plugins];

        Directory.CreateDirectory(Path.Combine(pluginsDirectory, "ignored"));

        var manifestDirectory = Path.Combine(pluginsDirectory, "included");
        Directory.CreateDirectory(manifestDirectory);
        WriteManifest(
            Path.Combine(manifestDirectory, "manifest.json"),
            new MoongatePluginManifest
            {
                Id = "included",
                Name = "Included",
                Version = "1.0.0",
                Authors = ["Squid"],
                EntryAssembly = "bin/Included.dll",
                EntryType = "Included.Plugin"
            }
        );

        var service = new PluginDiscoveryService(directoriesConfig);

        var discoveredPlugins = service.DiscoverPlugins();

        Assert.That(discoveredPlugins, Has.Count.EqualTo(1));
        Assert.That(discoveredPlugins[0].PluginId, Is.EqualTo("included"));
    }

    [Test]
    public void DiscoverPlugins_WhenEntryAssemblyMissing_ShouldThrowInvalidOperationException()
    {
        using var temp = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var pluginsDirectory = directoriesConfig[DirectoryType.Plugins];
        var pluginDirectory = Path.Combine(pluginsDirectory, "broken");

        Directory.CreateDirectory(pluginDirectory);
        WriteManifest(
            Path.Combine(pluginDirectory, "manifest.json"),
            new MoongatePluginManifest
            {
                Id = "broken",
                Name = "Broken",
                Version = "1.0.0",
                Authors = ["Squid"],
                EntryType = "Broken.Plugin"
            }
        );

        var service = new PluginDiscoveryService(directoriesConfig);

        Assert.Throws<InvalidOperationException>(() => service.DiscoverPlugins());
    }

    private static void WriteManifest(string path, MoongatePluginManifest manifest)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(manifest, MoongatePluginJsonContext.Default.MoongatePluginManifest)
        );
    }
}
