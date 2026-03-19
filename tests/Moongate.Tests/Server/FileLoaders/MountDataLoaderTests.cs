using Moongate.Server.Data.World;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.FileLoaders;

public sealed class MountDataLoaderTests
{
    [Test]
    public async Task LoadAsync_ShouldParseMountTilesFromUoConvertCfg()
    {
        using var temp = new TempDirectory();
        var cfgPath = Path.Combine(temp.Path, "uoconvert.cfg");
        await File.WriteAllTextAsync(
            cfgPath,
            """
            LOSOptions
            {
                UseNoShoot 0
            }

            Mounts
            {
                // duplicates should collapse
                Tiles 0x3EA0 0x3EAA 0x3EA0
            }

            StaticOptions
            {
                MaxStaticsPerBlock 1000
            }
            """
        );

        var mountTileData = new MountTileData();
        var loader = new MountDataLoader(mountTileData, cfgPath);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(mountTileData.Contains(0x3EA0), Is.True);
                Assert.That(mountTileData.Contains(0x3EAA), Is.True);
                Assert.That(mountTileData.ItemIds.Count, Is.EqualTo(2));
            }
        );
    }
}
