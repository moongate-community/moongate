using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Tests.Support;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Http.Mobiles;

/// <summary>
/// Content-addressed caching means a character who changes clothes leaves the old file behind, so
/// something has to take them away. The sweep's two properties matter equally: it removes what is
/// stale, and it keeps what is not.
/// </summary>
public class CharacterImageCacheSweeperTests
{
    [Fact]
    public void Sweep_RemovesAFileOlderThanTheWindow()
    {
        var world = new Fixture();
        var stale = world.Image("stale", DateTime.UtcNow.AddDays(-30));

        world.Sweeper.Sweep();

        Assert.False(File.Exists(stale));
    }

    // A sweep that deletes everything also passes a test that only checks deletion.
    [Fact]
    public void Sweep_KeepsAFileInsideTheWindow()
    {
        var world = new Fixture();
        var fresh = world.Image("fresh", DateTime.UtcNow);

        world.Sweeper.Sweep();

        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void Sweep_ReportsHowManyItRemoved()
    {
        var world = new Fixture();

        world.Image("old-one", DateTime.UtcNow.AddDays(-30));
        world.Image("old-two", DateTime.UtcNow.AddDays(-30));
        world.Image("kept", DateTime.UtcNow);

        Assert.Equal(2, world.Sweeper.Sweep());
    }

    [Fact]
    public void Sweep_OnAnEmptyDirectory_DoesNothing()
        => Assert.Equal(0, new Fixture().Sweeper.Sweep());

    private sealed class Fixture
    {
        private readonly string _cachePath;

        public Fixture()
        {
            var root = TemporaryDirectory.Create("mg-character-sweep-");

            Sweeper = new(new DirectoriesConfig(root, []));
            _cachePath = Path.Combine(root, "cache", "images", "characters");
        }

        public CharacterImageCacheSweeper Sweeper { get; }

        public string Image(string name, DateTime writtenUtc)
        {
            var path = Path.Combine(_cachePath, $"{name}.png");

            File.WriteAllBytes(path, [0x89, (byte)'P', (byte)'N', (byte)'G']);
            File.SetLastWriteTimeUtc(path, writtenUtc);

            return path;
        }
    }
}
