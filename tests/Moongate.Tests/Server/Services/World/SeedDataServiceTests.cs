using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.World;

public class SeedDataServiceTests
{
    private sealed class InMemorySignDataService : ISignDataService
    {
        private readonly IReadOnlyList<SignEntry> _entries;

        public InMemorySignDataService(IReadOnlyList<SignEntry> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<SignEntry> GetAllEntries()
            => _entries;

        public IReadOnlyList<SignEntry> GetEntriesByMap(int mapId)
            => [.. _entries.Where(entry => entry.MapId == mapId)];

        public void SetEntries(IReadOnlyList<SignEntry> entries)
            => throw new NotSupportedException();
    }

    private sealed class InMemoryDecorationDataService : IDecorationDataService
    {
        private readonly IReadOnlyList<DecorationEntry> _entries;

        public InMemoryDecorationDataService(IReadOnlyList<DecorationEntry> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<DecorationEntry> GetAllEntries()
            => _entries;

        public IReadOnlyList<DecorationEntry> GetEntriesByMap(int mapId)
            => [.. _entries.Where(entry => entry.MapId == mapId)];

        public void SetEntries(IReadOnlyList<DecorationEntry> entries)
            => throw new NotSupportedException();
    }

    private sealed class InMemoryLocationCatalogService : ILocationCatalogService
    {
        private readonly IReadOnlyList<WorldLocationEntry> _entries;

        public InMemoryLocationCatalogService(IReadOnlyList<WorldLocationEntry> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<WorldLocationEntry> GetAllLocations()
            => _entries;

        public void SetLocations(IReadOnlyList<WorldLocationEntry> locations)
            => throw new NotSupportedException();
    }

    private sealed class InMemoryDoorDataService : IDoorDataService
    {
        private readonly IReadOnlyList<DoorComponentEntry> _entries;

        public InMemoryDoorDataService(IReadOnlyList<DoorComponentEntry> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<DoorComponentEntry> GetAllEntries()
            => _entries;

        public void SetEntries(IReadOnlyList<DoorComponentEntry> entries)
            => throw new NotSupportedException();

        public bool TryGetToggleDefinition(int itemId, out DoorToggleDefinition definition)
        {
            _ = itemId;
            definition = default;

            return false;
        }
    }

    private sealed class InMemorySpawnsDataService : ISpawnsDataService
    {
        private readonly IReadOnlyList<SpawnDefinitionEntry> _entries;

        public InMemorySpawnsDataService(IReadOnlyList<SpawnDefinitionEntry> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<SpawnDefinitionEntry> GetAllEntries()
            => _entries;

        public IReadOnlyList<SpawnDefinitionEntry> GetEntriesByMap(int mapId)
            => [.. _entries.Where(entry => entry.MapId == mapId)];

        public void SetEntries(IReadOnlyList<SpawnDefinitionEntry> entries)
            => throw new NotSupportedException();
    }

    private sealed class InMemoryTeleportersDataService : ITeleportersDataService
    {
        private readonly IReadOnlyList<TeleporterEntry> _entries;

        public InMemoryTeleportersDataService(IReadOnlyList<TeleporterEntry> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<TeleporterEntry> GetAllEntries()
            => _entries;

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceMap(int mapId)
            => [.. _entries.Where(entry => entry.SourceMapId == mapId)];

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceSector(int mapId, int sectorX, int sectorY)
            => [
                .. _entries.Where(
                      entry =>
                          entry.SourceMapId == mapId &&
                          (entry.SourceLocation.X >> MapSectorConsts.SectorShift) == sectorX &&
                          (entry.SourceLocation.Y >> MapSectorConsts.SectorShift) == sectorY
                  )
            ];

        public bool TryGetEntryAtLocation(int mapId, Point3D location, out TeleporterEntry entry)
        {
            entry = _entries.FirstOrDefault(candidate => candidate.SourceMapId == mapId && candidate.SourceLocation == location);

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
            => throw new NotSupportedException();
    }

    [Test]
    public void GetDecorationsByMap_ShouldDelegateToDecorationDataService()
    {
        IReadOnlyList<DecorationEntry> decorations =
        [
            new(
                1,
                "Trammel",
                "sample.cfg",
                "Static",
                (Serial)0x00000450u,
                new Dictionary<string, string>(),
                new(10, 20, 0),
                string.Empty
            )
        ];
        var signDataService = new InMemorySignDataService([]);
        var decorationDataService = new InMemoryDecorationDataService(decorations);
        var doorDataService = new InMemoryDoorDataService([]);
        var spawnsDataService = new InMemorySpawnsDataService([]);
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(
            signDataService,
            decorationDataService,
            doorDataService,
            locationCatalogService,
            spawnsDataService,
            new InMemoryTeleportersDataService([])
        );

        var result = service.GetDecorationsByMap(1);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result[0].TypeName, Is.EqualTo("Static"));
                Assert.That(result[0].ItemId, Is.EqualTo((Serial)0x00000450u));
            }
        );
    }

    [Test]
    public void GetDoors_ShouldDelegateToDoorDataService()
    {
        IReadOnlyList<DoorComponentEntry> doors =
        [
            new(
                0,
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                0,
                "Test Door"
            )
        ];
        var signDataService = new InMemorySignDataService([]);
        var decorationDataService = new InMemoryDecorationDataService([]);
        var doorDataService = new InMemoryDoorDataService(doors);
        var spawnsDataService = new InMemorySpawnsDataService([]);
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(
            signDataService,
            decorationDataService,
            doorDataService,
            locationCatalogService,
            spawnsDataService,
            new InMemoryTeleportersDataService([])
        );

        var result = service.GetDoors();

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result[0].Category, Is.EqualTo(0));
                Assert.That(result[0].Comment, Is.EqualTo("Test Door"));
            }
        );
    }

    [Test]
    public void GetLocations_ShouldDelegateToLocationCatalogService()
    {
        IReadOnlyList<WorldLocationEntry> locations =
        [
            new(0, "Felucca", "Towns", "Britain", new(1496, 1628, 20))
        ];
        var signDataService = new InMemorySignDataService([]);
        var decorationDataService = new InMemoryDecorationDataService([]);
        var doorDataService = new InMemoryDoorDataService([]);
        var spawnsDataService = new InMemorySpawnsDataService([]);
        var locationCatalogService = new InMemoryLocationCatalogService(locations);
        var service = new SeedDataService(
            signDataService,
            decorationDataService,
            doorDataService,
            locationCatalogService,
            spawnsDataService,
            new InMemoryTeleportersDataService([])
        );

        var result = service.GetLocations();

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result[0].MapId, Is.EqualTo(0));
                Assert.That(result[0].Name, Is.EqualTo("Britain"));
            }
        );
    }

    [Test]
    public void GetSignsByMap_ShouldDelegateToSignDataService()
    {
        IReadOnlyList<SignEntry> signs =
        [
            new(0, 0, (Serial)0x00000BD8u, new(1, 2, 3), "#1016093"),
            new(1, 0, (Serial)0x00000BD9u, new(4, 5, 6), "Baker")
        ];
        var signDataService = new InMemorySignDataService(signs);
        var decorationDataService = new InMemoryDecorationDataService([]);
        var doorDataService = new InMemoryDoorDataService([]);
        var spawnsDataService = new InMemorySpawnsDataService([]);
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(
            signDataService,
            decorationDataService,
            doorDataService,
            locationCatalogService,
            spawnsDataService,
            new InMemoryTeleportersDataService([])
        );

        var map0 = service.GetSignsByMap(0);
        var map1 = service.GetSignsByMap(1);

        Assert.Multiple(
            () =>
            {
                Assert.That(map0, Has.Count.EqualTo(1));
                Assert.That(map0[0].Text, Is.EqualTo("#1016093"));
                Assert.That(map1, Has.Count.EqualTo(1));
                Assert.That(map1[0].Text, Is.EqualTo("Baker"));
            }
        );
    }

    [Test]
    public void GetSpawnsByMap_ShouldDelegateToSpawnsDataService()
    {
        IReadOnlyList<SpawnDefinitionEntry> spawns =
        [
            new(
                0,
                "Felucca",
                "shared/felucca",
                "Outdoors.json",
                Guid.Parse("001a5320-820c-4300-96f9-676e428b55be"),
                SpawnDefinitionKind.Spawner,
                "Spawner (213)",
                new(4066, 569, 0),
                8,
                TimeSpan.FromMinutes(20),
                TimeSpan.FromMinutes(20),
                0,
                80,
                80,
                [new("PolarBear", 8, 100)]
            )
        ];
        var signDataService = new InMemorySignDataService([]);
        var decorationDataService = new InMemoryDecorationDataService([]);
        var doorDataService = new InMemoryDoorDataService([]);
        var spawnsDataService = new InMemorySpawnsDataService(spawns);
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(
            signDataService,
            decorationDataService,
            doorDataService,
            locationCatalogService,
            spawnsDataService,
            new InMemoryTeleportersDataService([])
        );

        var result = service.GetSpawnsByMap(0);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result[0].MapId, Is.EqualTo(0));
                Assert.That(result[0].Entries, Has.Count.EqualTo(1));
                Assert.That(result[0].Entries[0].Name, Is.EqualTo("PolarBear"));
            }
        );
    }

    [Test]
    public void GetTeleportersBySourceMap_ShouldDelegateToTeleportersDataService()
    {
        IReadOnlyList<TeleporterEntry> teleporters =
        [
            new(0, "Felucca", new(311, 786, -24), 1, "Trammel", new(314, 784, 0), false)
        ];

        var service = new SeedDataService(
            new InMemorySignDataService([]),
            new InMemoryDecorationDataService([]),
            new InMemoryDoorDataService([]),
            new InMemoryLocationCatalogService([]),
            new InMemorySpawnsDataService([]),
            new InMemoryTeleportersDataService(teleporters)
        );

        var result = service.GetTeleportersBySourceMap(0);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result[0].SourceMapId, Is.EqualTo(0));
                Assert.That(result[0].DestinationMapId, Is.EqualTo(1));
                Assert.That(result[0].Back, Is.False);
            }
        );
    }
}
