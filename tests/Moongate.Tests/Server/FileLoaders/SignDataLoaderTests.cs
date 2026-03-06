using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.World;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.FileLoaders;

public class SignDataLoaderTests
{
    private sealed class TestSignDataService : ISignDataService
    {
        public List<SignEntry> Entries { get; } = [];

        public IReadOnlyList<SignEntry> GetAllEntries()
            => Entries;

        public IReadOnlyList<SignEntry> GetEntriesByMap(int mapId)
            => [..Entries.Where(entry => entry.MapId == mapId)];

        public void SetEntries(IReadOnlyList<SignEntry> entries)
        {
            Entries.Clear();
            Entries.AddRange(entries);
        }
    }

    [Test]
    public async Task LoadAsync_ShouldDuplicateMapCodeZeroToFeluccaAndTrammel()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var signsPath = Path.Combine(directories[DirectoryType.Data], "signs");
        Directory.CreateDirectory(signsPath);

        const string cfg = """
                           0 3032 373 904 -1 #1016093
                           """;
        await File.WriteAllTextAsync(Path.Combine(signsPath, "signs.cfg"), cfg);

        var service = new TestSignDataService();
        var loader = new SignDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(2));
                Assert.That(service.Entries.Select(static entry => entry.MapId), Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(service.Entries.All(static entry => entry.SourceMapCode == 0), Is.True);
                Assert.That(service.Entries.All(static entry => entry.ItemId == (Serial)3032u), Is.True);
                Assert.That(service.Entries[0].Location, Is.EqualTo(new Point3D(373, 904, -1)));
                Assert.That(service.Entries[0].Text, Is.EqualTo("#1016093"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_ShouldParsePlainTextWithSpaces()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var signsPath = Path.Combine(directories[DirectoryType.Data], "signs");
        Directory.CreateDirectory(signsPath);

        const string cfg = """
                           2 2979 3632 2537 0 The Shakin' Bakery
                           """;
        await File.WriteAllTextAsync(Path.Combine(signsPath, "signs.cfg"), cfg);

        var service = new TestSignDataService();
        var loader = new SignDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(1));
                Assert.That(service.Entries[0].MapId, Is.EqualTo(1));
                Assert.That(service.Entries[0].SourceMapCode, Is.EqualTo(2));
                Assert.That(service.Entries[0].ItemId, Is.EqualTo((Serial)2979u));
                Assert.That(service.Entries[0].Text, Is.EqualTo("The Shakin' Bakery"));
            }
        );
    }
}
