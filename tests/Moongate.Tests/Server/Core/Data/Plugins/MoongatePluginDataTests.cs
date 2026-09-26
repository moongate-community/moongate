using Moongate.Server.Core.Data.Plugins;

namespace Moongate.Tests.Server.Core.Data.Plugins;

public sealed class MoongatePluginDataTests
{
    [Fact]
    public void Constructor_RejectsDuplicateDependencyIdsIgnoringCase()
    {
        Assert.Throws<ArgumentException>(() => new MoongatePluginData(
                "plugin",
                "Plugin",
                new(1, 0, 0),
                dependencies:
                [new("core"), new("CORE")]
            )
        );
    }

    [Theory, InlineData(null), InlineData(""), InlineData(" ")]
    public void Constructor_RejectsInvalidId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new MoongatePluginData(id!, "Plugin", new(1, 0, 0))
        );
    }

    [Theory, InlineData(null), InlineData(""), InlineData(" ")]
    public void Constructor_RejectsInvalidName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new MoongatePluginData("plugin", name!, new(1, 0, 0))
        );
    }

    [Fact]
    public void Constructor_RejectsNullDependency()
    {
        Assert.Throws<ArgumentException>(() => new MoongatePluginData(
                "plugin",
                "Plugin",
                new(1, 0, 0),
                dependencies: [null!]
            )
        );
    }

    [Fact]
    public void Constructor_RejectsNullVersion()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MoongatePluginData("plugin", "Plugin", null!)
        );
    }

    [Fact]
    public void Constructor_SnapshotsDependenciesWithoutNormalizingText()
    {
        var dependency = new MoongatePluginDependencyData("Core", new(1, 0, 0));
        var source = new List<MoongatePluginDependencyData> { dependency };
        var data = new MoongatePluginData(
            " Example ",
            " Example plugin ",
            new(2, 0, 0),
            "",
            " ",
            source
        );
        source.Clear();

        Assert.Same(dependency, Assert.Single(data.Dependencies));
        Assert.Equal(" Example ", data.Id);
        Assert.Equal(" Example plugin ", data.Name);
        Assert.Equal("", data.Author);
        Assert.Equal(" ", data.Description);
        var list = Assert.IsAssignableFrom<IList<MoongatePluginDependencyData>>(data.Dependencies);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }
}
