using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.World;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.FileLoaders;

public class SpawnsDataLoaderTests
{
    private sealed class TestSpawnsDataService : ISpawnsDataService
    {
        public List<SpawnDefinitionEntry> Entries { get; } = [];

        public IReadOnlyList<SpawnDefinitionEntry> GetAllEntries()
            => Entries;

        public IReadOnlyList<SpawnDefinitionEntry> GetEntriesByMap(int mapId)
            => [..Entries.Where(entry => entry.MapId == mapId)];

        public void SetEntries(IReadOnlyList<SpawnDefinitionEntry> entries)
        {
            Entries.Clear();
            Entries.AddRange(entries);
        }
    }

    [Test]
    public async Task LoadAsync_ShouldImportProximitySpawnerEntries()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var spawnsPath = Path.Combine(directories[DirectoryType.Data], "spawns", "shared", "felucca");
        Directory.CreateDirectory(spawnsPath);

        const string json = """
                            [
                              {
                                "type": "ProximitySpawner",
                                "guid": "11111111-2222-3333-4444-555555555555",
                                "name": "Proximity Spawner",
                                "location": [100, 200, 0],
                                "map": "Felucca",
                                "count": 1,
                                "minDelay": "00:00:30",
                                "maxDelay": "00:01:00",
                                "team": 0,
                                "homeRange": 6,
                                "walkingRange": 6,
                                "entries": [
                                  { "name": "Rat", "maxCount": 1, "probability": 100 }
                                ]
                              }
                            ]
                            """;
        await File.WriteAllTextAsync(Path.Combine(spawnsPath, "proximity.json"), json);

        var service = new TestSpawnsDataService();
        var loader = new SpawnsDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(1));
                Assert.That(service.Entries[0].Kind, Is.EqualTo(SpawnDefinitionKind.ProximitySpawner));
                Assert.That(service.Entries[0].HomeRange, Is.EqualTo(6));
                Assert.That(service.Entries[0].Entries[0].Name, Is.EqualTo("Rat"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_ShouldImportSpawnEntriesFromJsonFiles()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var spawnsPath = Path.Combine(directories[DirectoryType.Data], "spawns", "shared", "felucca");
        Directory.CreateDirectory(spawnsPath);

        const string json = """
                            [
                              {
                                "type": "Spawner",
                                "guid": "001a5320-820c-4300-96f9-676e428b55be",
                                "name": "Spawner (213)",
                                "location": [4066, 569, 0],
                                "map": "Felucca",
                                "count": 8,
                                "minDelay": "00:20:00",
                                "maxDelay": "00:20:00",
                                "team": 0,
                                "homeRange": 80,
                                "walkingRange": 80,
                                "entries": [
                                  { "name": "PolarBear", "maxCount": 8, "probability": 100 },
                                  { "name": "Walrus", "maxCount": 8, "probability": 100 }
                                ]
                              }
                            ]
                            """;
        await File.WriteAllTextAsync(Path.Combine(spawnsPath, "Outdoors.json"), json);

        var service = new TestSpawnsDataService();
        var loader = new SpawnsDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.Entries, Has.Count.EqualTo(1));
                Assert.That(service.Entries[0].MapId, Is.EqualTo(0));
                Assert.That(service.Entries[0].Map, Is.EqualTo("Felucca"));
                Assert.That(service.Entries[0].Kind, Is.EqualTo(SpawnDefinitionKind.Spawner));
                Assert.That(service.Entries[0].Location.X, Is.EqualTo(4066));
                Assert.That(service.Entries[0].Location.Y, Is.EqualTo(569));
                Assert.That(service.Entries[0].Location.Z, Is.EqualTo(0));
                Assert.That(service.Entries[0].Entries, Has.Count.EqualTo(2));
                Assert.That(service.Entries[0].Entries[0].Name, Is.EqualTo("PolarBear"));
                Assert.That(service.Entries[0].Entries[0].MaxCount, Is.EqualTo(8));
                Assert.That(service.Entries[0].Entries[0].Probability, Is.EqualTo(100));
            }
        );
    }

    [Test]
    public async Task LoadAsync_ShouldSkipEntriesWithUnknownMapName()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var spawnsPath = Path.Combine(directories[DirectoryType.Data], "spawns", "shared", "custom");
        Directory.CreateDirectory(spawnsPath);

        const string json = """
                            [
                              {
                                "type": "Spawner",
                                "guid": "8b89e2f0-f83f-4f4d-a076-c540625ab2cb",
                                "name": "Spawner (Custom)",
                                "location": [1, 2, 3],
                                "map": "UnknownMap",
                                "count": 1,
                                "minDelay": "00:00:10",
                                "maxDelay": "00:00:20",
                                "team": 0,
                                "homeRange": 5,
                                "walkingRange": 5,
                                "entries": [
                                  { "name": "Rat", "maxCount": 1, "probability": 100 }
                                ]
                              }
                            ]
                            """;
        await File.WriteAllTextAsync(Path.Combine(spawnsPath, "custom.json"), json);

        var service = new TestSpawnsDataService();
        var loader = new SpawnsDataLoader(directories, service);

        await loader.LoadAsync();

        Assert.That(service.Entries, Is.Empty);
    }
}
