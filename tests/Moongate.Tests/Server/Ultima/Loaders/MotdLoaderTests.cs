using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class MotdLoaderTests
{
    [Fact]
    public async Task MissingFile_ReturnsNoLinesAndFreezesRegistry()
    {
        using var root = new TemporaryDirectory();
        var registry = new MotdVariableRegistry();
        var loader = CreateLoader(root, registry);

        await loader.InitializeAsync();
        Assert.Empty((await loader.LoadDataAsync()).Entities);
        Assert.Throws<InvalidOperationException>(() => registry.Register("later", (_, _) => ValueTask.FromResult("late")));
    }

    [Fact]
    public async Task ValidFile_PreservesOrderAndFileIndexes_AndAcceptsPluginVariable()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/motd.toml", "lines = [\"First ${custom}\", \"  \", \"Third\"]\n");
        var registry = new MotdVariableRegistry();
        registry.Register("custom", (_, _) => ValueTask.FromResult("ok"));

        var lines = (await CreateLoader(root, registry).LoadDataAsync()).Entities;

        Assert.Equal(2, lines.Count);
        Assert.Equal((1, "First ${custom}"), (lines[0].Index, lines[0].Template));
        Assert.Equal((3, "Third"), (lines[1].Index, lines[1].Template));
    }

    [Theory]
    [InlineData("# empty\n")]
    [InlineData("other = 1\n")]
    public async Task MissingLines_FailsWithPath(string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/motd.toml", toml);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
        Assert.Contains("motd.toml", exception.Message);
    }

    [Theory]
    [InlineData("lines = [\"${missing}\"]\n", "missing")]
    [InlineData("lines = [\"${INVALID_NAME}\"]\n", "INVALID_NAME")]
    [InlineData("lines = [\"a\\u0000b\"]\n", "NUL")]
    public async Task InvalidLine_FailsWithPathAndIndex(string toml, string detail)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/motd.toml", toml);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
        Assert.Contains("motd.toml", exception.Message);
        Assert.Contains("line 1", exception.Message);
        Assert.Contains(detail, exception.Message);
    }

    [Fact]
    public async Task MalformedToml_FailsWithPath()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/motd.toml", "lines = [\"unterminated\n");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
        Assert.Contains("motd.toml", exception.Message);
    }

    [Fact]
    public async Task OversizedLiteralLine_FailsAtStartupWithIndex()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/motd.toml", $"lines = [\"{new string('x', 32744)}\"]\n");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
        Assert.Contains("line 1", exception.Message);
    }

    private static MotdLoader CreateLoader(TemporaryDirectory root, MotdVariableRegistry? registry = null)
    {
        return new MotdLoader(new DirectoriesConfig(root.Path, ["data"]), registry ?? new MotdVariableRegistry());
    }
}
