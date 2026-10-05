using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class DecorationsLoaderTests
{
    private const string Door =
        "[[decoration]]\ncomment = \"metal door\"\ntype = \"MetalDoor\"\nitem_id = 0x0675\n" +
        "props = { facing = \"west_cw\", hue = 5, locked = true, point_dest = [1, 2, 3], keys = [1, 2] }\n" +
        "locations = [[1411, 1621, 30], [1411, 1622, -5]]\n";

    [Fact]
    public async Task LoadAsync_SkipsUnderscoreFolders_ButLoadsUnderscoreFiles_AndResolvesTheMaps()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/decorations/britannia/britain.toml", Door);
        root.CreateFile("templates/decorations/britannia/_covetous.toml", Door);
        root.CreateFile("templates/decorations/malas/luna.toml", Door);
        root.CreateFile("templates/decorations/_bounty_boards/boards.toml", "this is not toml");

        var files = await CreateLoader(root).LoadAsync();

        Assert.Equal(
            ["britannia/_covetous", "britannia/britain", "malas/luna"],
            files.Select(file => $"{file.Folder}/{file.Name}")
        );
        Assert.Equal([MapType.Trammel, MapType.Felucca], files[1].Maps);
        Assert.Equal([MapType.Malas], files[2].Maps);
    }

    [Fact]
    public async Task LoadAsync_ReadsTheBlocks_KeepingTheScalarPropsAndThePoints()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/decorations/trammel/doors.toml", Door + "\n[[decoration]]\ntype = \"AnvilEastAddon\"\nlocations = [[1, 2, 3]]\n");

        var blocks = (await CreateLoader(root).LoadAsync()).Single().Blocks;

        var door = blocks[0];
        Assert.Equal(("MetalDoor", "metal door", (int?)0x0675), (door.Type, door.Comment, door.ItemId));
        Assert.Equal(
            new Dictionary<string, object>
            {
                ["facing"] = "west_cw", ["hue"] = 5L, ["locked"] = true, ["point_dest"] = new Point3D(1, 2, 3)
            },
            door.Props
        );
        Assert.Equal([new Point3D(1411, 1621, 30), new Point3D(1411, 1622, -5)], door.Locations);

        var addon = blocks[1];
        Assert.Equal(("AnvilEastAddon", (string?)null, (int?)null), (addon.Type, addon.Comment, addon.ItemId));
        Assert.Empty(addon.Props);
    }

    [Fact]
    public async Task LoadAsync_AnUnknownFolder_ThrowsNamingIt()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/decorations/sosaria/britain.toml", Door);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadAsync());

        Assert.Contains("sosaria", error.Message);
    }

    [Fact]
    public async Task LoadAsync_NoDirectory_ReturnsNoFiles()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty(await CreateLoader(root).LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_TheShippedDecorations_LoadWithoutTheUnderscoreSets()
    {
        var files = await new DecorationsLoader(new DirectoriesConfig(RepositoryRoot(), ["templates"])).LoadAsync();

        Assert.DoesNotContain(files, file => file.Folder.StartsWith('_'));
        Assert.Equal(33463, files.Sum(file => file.Blocks.Sum(block => block.Locations.Count)));
    }

    private static DecorationsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["templates"]));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root");
    }
}
