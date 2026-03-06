using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Ids;

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
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(signDataService, decorationDataService, doorDataService, locationCatalogService);

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
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(signDataService, decorationDataService, doorDataService, locationCatalogService);

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
        var locationCatalogService = new InMemoryLocationCatalogService(locations);
        var service = new SeedDataService(signDataService, decorationDataService, doorDataService, locationCatalogService);

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
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(signDataService, decorationDataService, doorDataService, locationCatalogService);

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
}
