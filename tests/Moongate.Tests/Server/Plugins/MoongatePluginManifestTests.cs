using System.Text.Json;
using Moongate.Server.Json;

namespace Moongate.Tests.Server.Plugins;

public class MoongatePluginManifestTests
{
    [Test]
    public void Deserialize_DependencyMetadata_ShouldPopulateOptionalAndVersionRange()
    {
        const string json = """
                            {
                              "id": "my-plugin",
                              "name": "My Plugin",
                              "version": "1.0.0",
                              "authors": ["Squid"],
                              "entryAssembly": "bin/MyPlugin.dll",
                              "entryType": "MyPlugin.MyPlugin",
                              "dependencies": [
                                {
                                  "id": "moongate.dialogue",
                                  "versionRange": ">=1.0.0",
                                  "optional": true
                                }
                              ]
                            }
                            """;

        var manifest = JsonSerializer.Deserialize(
            json,
            MoongatePluginJsonContext.Default.MoongatePluginManifest
        );

        Assert.That(manifest, Is.Not.Null);
        Assert.That(manifest!.Dependencies, Has.Count.EqualTo(1));
        Assert.That(manifest.Dependencies[0].Id, Is.EqualTo("moongate.dialogue"));
        Assert.That(manifest.Dependencies[0].VersionRange, Is.EqualTo(">=1.0.0"));
        Assert.That(manifest.Dependencies[0].Optional, Is.True);
    }

    [Test]
    public void Deserialize_EmptyAuthors_ShouldPreserveEmptyList()
    {
        const string json = """
                            {
                              "id": "my-plugin",
                              "name": "My Plugin",
                              "version": "1.0.0",
                              "authors": [],
                              "entryAssembly": "bin/MyPlugin.dll",
                              "entryType": "MyPlugin.MyPlugin"
                            }
                            """;

        var manifest = JsonSerializer.Deserialize(
            json,
            MoongatePluginJsonContext.Default.MoongatePluginManifest
        );

        Assert.That(manifest, Is.Not.Null);
        Assert.That(manifest!.Authors, Is.Empty);
    }

    [Test]
    public void Deserialize_MissingId_ShouldLeaveIdUnset()
    {
        const string json = """
                            {
                              "name": "My Plugin",
                              "version": "1.0.0",
                              "authors": [],
                              "entryAssembly": "bin/MyPlugin.dll",
                              "entryType": "MyPlugin.MyPlugin"
                            }
                            """;

        var manifest = JsonSerializer.Deserialize(
            json,
            MoongatePluginJsonContext.Default.MoongatePluginManifest
        );

        Assert.That(manifest, Is.Not.Null);
        Assert.That(manifest!.Id, Is.Null);
    }

    [Test]
    public void Deserialize_ValidManifest_ShouldPopulateAllFields()
    {
        const string json = """
                            {
                              "id": "my-plugin",
                              "name": "My Plugin",
                              "version": "1.0.0",
                              "authors": ["Squid"],
                              "description": "Adds custom world content.",
                              "entryAssembly": "bin/MyPlugin.dll",
                              "entryType": "MyPlugin.MyPlugin",
                              "dependencies": [
                                {
                                  "id": "moongate.dialogue",
                                  "versionRange": ">=1.0.0",
                                  "optional": false
                                }
                              ]
                            }
                            """;

        var manifest = JsonSerializer.Deserialize(
            json,
            MoongatePluginJsonContext.Default.MoongatePluginManifest
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(manifest, Is.Not.Null);
                Assert.That(manifest!.Id, Is.EqualTo("my-plugin"));
                Assert.That(manifest.Name, Is.EqualTo("My Plugin"));
                Assert.That(manifest.Version, Is.EqualTo("1.0.0"));
                Assert.That(manifest.Authors, Is.EqualTo(["Squid"]));
                Assert.That(manifest.Description, Is.EqualTo("Adds custom world content."));
                Assert.That(manifest.EntryAssembly, Is.EqualTo("bin/MyPlugin.dll"));
                Assert.That(manifest.EntryType, Is.EqualTo("MyPlugin.MyPlugin"));
                Assert.That(manifest.Dependencies, Has.Count.EqualTo(1));
                Assert.That(manifest.Dependencies[0].Id, Is.EqualTo("moongate.dialogue"));
                Assert.That(manifest.Dependencies[0].VersionRange, Is.EqualTo(">=1.0.0"));
                Assert.That(manifest.Dependencies[0].Optional, Is.False);
            }
        );
    }
}
