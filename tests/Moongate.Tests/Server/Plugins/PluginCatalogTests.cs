using DryIoc;
using Moongate.Server.Services.Plugins;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Tests.Server.Plugins;

public class PluginCatalogTests
{
    [Fact]
    public void Record_KeepsTheMetadataAndTheDeclaringAssembly()
    {
        var catalog = new PluginCatalog();

        catalog.Record(new FakePlugin("fake.one", "Fake One"), isExternal: false);

        var plugin = Assert.Single(catalog.Plugins);

        Assert.Equal("fake.one", plugin.Id);
        Assert.Equal("Fake One", plugin.Name);
        Assert.Equal(new Version(2, 3, 4), plugin.Version);
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

        catalog.Record(new FakePlugin("fake.external", "External"), isExternal: true);

        Assert.True(Assert.Single(catalog.Plugins).IsExternal);
    }

    [Fact]
    public void Record_IgnoresASecondRecordOfTheSameId()
    {
        // FromDirectory-loaded plugins are found by elimination after the explicit ones are recorded, so
        // the same plugin reaching Record twice is a real path, not a hypothetical.
        var catalog = new PluginCatalog();

        catalog.Record(new FakePlugin("fake.one", "First"), isExternal: false);
        catalog.Record(new FakePlugin("fake.one", "Second"), isExternal: true);

        var plugin = Assert.Single(catalog.Plugins);

        Assert.Equal("First", plugin.Name);
        Assert.False(plugin.IsExternal);
    }

    [Fact]
    public void Plugins_KeepsTheOrderTheyWereRecordedIn()
    {
        var catalog = new PluginCatalog();

        catalog.Record(new FakePlugin("fake.one", "One"), isExternal: false);
        catalog.Record(new FakePlugin("fake.two", "Two"), isExternal: false);

        Assert.Equal(["fake.one", "fake.two"], catalog.Plugins.Select(plugin => plugin.Id));
    }

    private sealed class FakePlugin : ISquidStdPlugin
    {
        private readonly PluginMetadata _metadata;

        public FakePlugin(string id, string name)
        {
            _metadata = new()
            {
                Id = id,
                Name = name,
                Version = new(2, 3, 4),
                Author = "squid",
                Description = "a fake"
            };
        }

        public PluginMetadata Metadata => _metadata;

        public void Configure(IContainer container, PluginContext context)
        {
        }
    }
}
