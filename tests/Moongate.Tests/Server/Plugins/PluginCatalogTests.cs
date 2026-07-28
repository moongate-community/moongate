using DryIoc;
using Moongate.Server.Services.Plugins;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Tests.Server.Plugins;

public class PluginCatalogTests
{
    private sealed class FakePlugin : ISquidStdPlugin
    {
        public FakePlugin(string id, string name)
        {
            Metadata = new()
            {
                Id = id,
                Name = name,
                Version = new(2, 3, 4),
                Author = "squid",
                Description = "a fake"
            };
        }

        public PluginMetadata Metadata { get; }

        public void Configure(IContainer container, PluginContext context) { }
    }

    [Fact]
    public void Plugins_KeepsTheOrderTheyWereRecordedIn()
    {
        var catalog = new PluginCatalog();

        catalog.Record(new FakePlugin("fake.one", "One"), false);
        catalog.Record(new FakePlugin("fake.two", "Two"), false);

        Assert.Equal(["fake.one", "fake.two"], catalog.Plugins.Select(plugin => plugin.Id));
    }

    [Fact]
    public void Record_IgnoresASecondRecordOfTheSameId()
    {
        // FromDirectory-loaded plugins are found by elimination after the explicit ones are recorded, so
        // the same plugin reaching Record twice is a real path, not a hypothetical.
        var catalog = new PluginCatalog();

        catalog.Record(new FakePlugin("fake.one", "First"), false);
        catalog.Record(new FakePlugin("fake.one", "Second"), true);

        var plugin = Assert.Single(catalog.Plugins);

        Assert.Equal("First", plugin.Name);
        Assert.False(plugin.IsExternal);
    }

    [Fact]
    public void Record_KeepsTheMetadataAndTheDeclaringAssembly()
    {
        var catalog = new PluginCatalog();

        catalog.Record(new FakePlugin("fake.one", "Fake One"), false);

        var plugin = Assert.Single(catalog.Plugins);

        Assert.Equal("fake.one", plugin.Id);
        Assert.Equal("Fake One", plugin.Name);
        Assert.Equal(new(2, 3, 4), plugin.Version);
        Assert.Equal("squid", plugin.Author);
        Assert.Equal("a fake", plugin.Description);
        Assert.False(plugin.IsExternal);

        // The assembly name is the join key the route inspector uses; it must come from the plugin's own
        // type, not from anything the plugin declares about itself.
        Assert.Equal(typeof(PluginCatalogTests).Assembly.GetName().Name, plugin.AssemblyName);
    }

    [Fact]
    public void Record_MarksExternalPluginsAsSuch()
    {
        var catalog = new PluginCatalog();

        catalog.Record(new FakePlugin("fake.external", "External"), true);

        Assert.True(Assert.Single(catalog.Plugins).IsExternal);
    }
}
