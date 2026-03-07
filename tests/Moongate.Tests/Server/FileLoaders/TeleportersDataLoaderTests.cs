using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.World;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.FileLoaders;

public class TeleportersDataLoaderTests
{
    private sealed class TestTeleportersDataService : ITeleportersDataService
    {
        public List<TeleporterEntry> Entries { get; } = [];

        public IReadOnlyList<TeleporterEntry> GetAllEntries()
            => Entries;

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceMap(int mapId)
            => [..Entries.Where(entry => entry.SourceMapId == mapId)];

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceSector(int mapId, int sectorX, int sectorY)
            => [
                ..Entries.Where(
                      entry =>
                          entry.SourceMapId == mapId &&
                          (entry.SourceLocation.X >> MapSectorConsts.SectorShift) == sectorX &&
                          (entry.SourceLocation.Y >> MapSectorConsts.SectorShift) == sectorY
                  )
            ];

        public bool TryGetEntryAtLocation(int mapId, Point3D location, out TeleporterEntry entry)
        {
            entry = Entries.FirstOrDefault(candidate => candidate.SourceMapId == mapId && candidate.SourceLocation == location);

            return entry != default;
        }

        public bool TryResolveTeleportDestination(
            int mapId,
            Point3D location,
            out int destinationMapId,
            out Point3D destinationLocation,
            int maxHops = 4
        )
        {
            _ = maxHops;

            if (TryGetEntryAtLocation(mapId, location, out var entry))
            {
                destinationMapId = entry.DestinationMapId;
                destinationLocation = entry.DestinationLocation;

                return true;
            }

            destinationMapId = mapId;
            destinationLocation = location;

            return false;
        }

        public void SetEntries(IReadOnlyList<TeleporterEntry> entries)
        {
            Entries.Clear();
            Entries.AddRange(entries);
        }
    }

    [Test]
    public async Task LoadAsync_ShouldImportTeleportersAndResolveMapIds()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var teleportersPath = Path.Combine(directories[DirectoryType.Data], "teleporters");
        Directory.CreateDirectory(teleportersPath);

        const string json = """
                            [
                              {
                                "src": { "map": "Felucca", "loc": [311, 786, -24] },
                                "dst": { "map": "Trammel", "loc": [314, 784, 0] },
                                "back": false
                              }
                            ]
                            """;
        await File.WriteAllTextAsync(Path.Combine(teleportersPath, "teleporters.json"), json);

        var service = new TestTeleportersDataService();
        var loader = new TeleportersDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(1));
                Assert.That(service.Entries[0].SourceMapId, Is.EqualTo(0));
                Assert.That(service.Entries[0].DestinationMapId, Is.EqualTo(1));
                Assert.That(service.Entries[0].SourceLocation.X, Is.EqualTo(311));
                Assert.That(service.Entries[0].SourceLocation.Y, Is.EqualTo(786));
                Assert.That(service.Entries[0].SourceLocation.Z, Is.EqualTo(-24));
                Assert.That(service.Entries[0].DestinationLocation.X, Is.EqualTo(314));
                Assert.That(service.Entries[0].DestinationLocation.Y, Is.EqualTo(784));
                Assert.That(service.Entries[0].DestinationLocation.Z, Is.EqualTo(0));
                Assert.That(service.Entries[0].Back, Is.False);
            }
        );
    }

    [Test]
    public async Task LoadAsync_ShouldSkipTeleportersWithUnknownMapName()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var teleportersPath = Path.Combine(directories[DirectoryType.Data], "teleporters");
        Directory.CreateDirectory(teleportersPath);

        const string json = """
                            [
                              {
                                "src": { "map": "Unknown", "loc": [311, 786, -24] },
                                "dst": { "map": "Felucca", "loc": [314, 784, 0] },
                                "back": false
                              }
                            ]
                            """;
        await File.WriteAllTextAsync(Path.Combine(teleportersPath, "teleporters.json"), json);

        var service = new TestTeleportersDataService();
        var loader = new TeleportersDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.That(service.Entries, Is.Empty);
    }
}
