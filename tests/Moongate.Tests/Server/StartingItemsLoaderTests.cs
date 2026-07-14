using Moongate.Server.Loaders;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class StartingItemsLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegisters()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var service = new StartingItemsService();
        var loader = new StartingItemsLoader(service, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "starting_items.yaml")));

            // The embedded table is loaded: the universal kit resolves for any body.
            var kit = service.Resolve(RaceType.Human, GenderType.Male, []);
            Assert.NotEmpty(kit.Pack);
            Assert.NotEmpty(kit.Equip); // Human/Male body clothing
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-startitems-" + Guid.NewGuid().ToString("N"));
}
