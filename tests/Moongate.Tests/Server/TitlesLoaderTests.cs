using Moongate.Server.Loaders;
using Moongate.Server.Services;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class TitlesLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-titles-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndResolvesRealTitles()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var titles = new TitleService();
        var loader = new TitlesLoader(titles, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "titles.yaml")));
            Assert.Equal(5, titles.Count);
            Assert.Equal("The Glorious Lord Bob", titles.GetTitle("Bob", 12000, 12000, female: false));
            Assert.Equal("The Dread Lady Bob", titles.GetTitle("Bob", 12000, -10000, female: true));
            Assert.Equal("Bob", titles.GetTitle("Bob", 0, 0, female: false));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPresent_LoadsExistingWithoutReseeding()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var dataDir = directories.RegisterDirectory("data");
        File.WriteAllText(
            Path.Combine(dataDir, "titles.yaml"),
            "- Fame: 10000\n  Karma:\n  - Karma: 10000\n    Title: The Custom {1} {0}\n"
        );
        var titles = new TitleService();
        var loader = new TitlesLoader(titles, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, titles.Count);
            Assert.Equal("The Custom Lady Bob", titles.GetTitle("Bob", 99999, 99999, female: true));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
