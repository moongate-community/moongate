using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.World;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.FileLoaders;

public class DoorDataLoaderTests
{
    private sealed class TestDoorDataService : IDoorDataService
    {
        public List<DoorComponentEntry> Entries { get; } = [];

        public IReadOnlyList<DoorComponentEntry> GetAllEntries()
            => Entries;

        public void SetEntries(IReadOnlyList<DoorComponentEntry> entries)
        {
            Entries.Clear();
            Entries.AddRange(entries);
        }

        public bool TryGetToggleDefinition(int itemId, out DoorToggleDefinition definition)
        {
            _ = itemId;
            definition = default;

            return false;
        }
    }

    [Test]
    public async Task LoadAsync_ShouldParseDoorsComponentsFile()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var componentsPath = Path.Combine(directories[DirectoryType.Data], "components");
        Directory.CreateDirectory(componentsPath);

        const string content = """
                               int	int	int	int	int	int	int	int	int	int	string

                               Category	Piece1	Piece2	Piece3	Piece4	Piece5	Piece6	Piece7	Piece8	FeatureMask	Comment

                               0	1657	1659	1653	1655	1661	1663	1665	1667	0	Metal Door
                               1	1689	1691	1685	1687	1693	1695	1697	1699	0	Barred Door
                               """;
        await File.WriteAllTextAsync(Path.Combine(componentsPath, "doors.txt"), content);

        var service = new TestDoorDataService();
        var loader = new DoorDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(2));
                Assert.That(service.Entries[0].Category, Is.EqualTo(0));
                Assert.That(service.Entries[0].Piece3, Is.EqualTo(1653));
                Assert.That(service.Entries[0].Comment, Is.EqualTo("Metal Door"));
            }
        );
    }

    [Test]
    public void LoadAsync_WhenLineIsInvalid_ShouldThrow()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var componentsPath = Path.Combine(directories[DirectoryType.Data], "components");
        Directory.CreateDirectory(componentsPath);

        const string content = """
                               Category Piece1 Piece2
                               0 1657 1659
                               """;
        File.WriteAllText(Path.Combine(componentsPath, "doors.txt"), content);

        var service = new TestDoorDataService();
        var loader = new DoorDataLoader(directories, service);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }
}
