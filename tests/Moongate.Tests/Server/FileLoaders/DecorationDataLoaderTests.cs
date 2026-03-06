using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.World;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.FileLoaders;

public class DecorationDataLoaderTests
{
    private sealed class TestDecorationDataService : IDecorationDataService
    {
        public List<DecorationEntry> Entries { get; } = [];

        public IReadOnlyList<DecorationEntry> GetAllEntries()
            => Entries;

        public IReadOnlyList<DecorationEntry> GetEntriesByMap(int mapId)
            => [.. Entries.Where(entry => entry.MapId == mapId)];

        public void SetEntries(IReadOnlyList<DecorationEntry> entries)
        {
            Entries.Clear();
            Entries.AddRange(entries);
        }
    }

    [Test]
    public async Task LoadAsync_ShouldDuplicateBritanniaEntriesForFeluccaAndTrammel()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var britanniaPath = Path.Combine(directories[DirectoryType.Data], "decoration", "Britannia");
        Directory.CreateDirectory(britanniaPath);

        const string cfg = """
                           Static 0x0063
                           1517 1670 20
                           """;
        await File.WriteAllTextAsync(Path.Combine(britanniaPath, "britain.cfg"), cfg);

        var service = new TestDecorationDataService();
        var loader = new DecorationDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(2));
                Assert.That(service.Entries.Select(static entry => entry.MapId), Is.EquivalentTo(new[] { 0, 1 }));
            }
        );
    }

    [Test]
    public async Task LoadAsync_ShouldParseCfgAndMapToFolderMapId()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var britanniaPath = Path.Combine(directories[DirectoryType.Data], "decoration", "Trammel");
        Directory.CreateDirectory(britanniaPath);

        const string cfg = """
                           # wall
                           Static 0x0450 (Hue=0x482;Name=Test)
                           10 20 0
                           11 21 1 extra_data

                           StoneFireplaceSouthAddon
                           15 25 0
                           """;
        await File.WriteAllTextAsync(Path.Combine(britanniaPath, "sample.cfg"), cfg);

        var service = new TestDecorationDataService();
        var loader = new DecorationDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(3));
                Assert.That(service.Entries.All(static entry => entry.MapId == 1), Is.True);
                Assert.That(service.Entries[0].TypeName, Is.EqualTo("Static"));
                Assert.That(service.Entries[0].ItemId, Is.EqualTo((Serial)0x00000450u));
                Assert.That(service.Entries[0].Parameters["Hue"], Is.EqualTo("0x482"));
                Assert.That(service.Entries[0].Parameters["Name"], Is.EqualTo("Test"));
                Assert.That(service.Entries[0].Location.X, Is.EqualTo(10));
                Assert.That(service.Entries[0].Location.Y, Is.EqualTo(20));
                Assert.That(service.Entries[0].Location.Z, Is.EqualTo(0));
                Assert.That(service.Entries[1].Extra, Is.EqualTo("extra_data"));
                Assert.That(service.Entries[2].TypeName, Is.EqualTo("StoneFireplaceSouthAddon"));
                Assert.That(service.Entries[2].ItemId, Is.EqualTo(Serial.Zero));
            }
        );
    }
}
