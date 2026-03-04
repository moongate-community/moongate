using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.World;

public class SeedDataServiceTests
{
    [Test]
    public void GetSignsByMap_ShouldDelegateToSignDataService()
    {
        IReadOnlyList<SignEntry> signs =
        [
            new SignEntry(0, 0, (Serial)0x00000BD8u, new(1, 2, 3), "#1016093"),
            new SignEntry(1, 0, (Serial)0x00000BD9u, new(4, 5, 6), "Baker")
        ];
        var signDataService = new InMemorySignDataService(signs);
        var decorationDataService = new InMemoryDecorationDataService([]);
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(signDataService, decorationDataService, locationCatalogService);

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
    public void GetDecorationsByMap_ShouldDelegateToDecorationDataService()
    {
        IReadOnlyList<DecorationEntry> decorations =
        [
            new DecorationEntry(
                1,
                "Trammel",
                "sample.cfg",
                "Static",
                (Serial)0x00000450u,
                [],
                new Point3D(10, 20, 0),
                string.Empty
            )
        ];
        var signDataService = new InMemorySignDataService([]);
        var decorationDataService = new InMemoryDecorationDataService(decorations);
        var locationCatalogService = new InMemoryLocationCatalogService([]);
        var service = new SeedDataService(signDataService, decorationDataService, locationCatalogService);

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
    public void GetLocations_ShouldDelegateToLocationCatalogService()
    {
        IReadOnlyList<WorldLocationEntry> locations =
        [
            new WorldLocationEntry(0, "Felucca", "Towns", "Britain", new(1496, 1628, 20))
        ];
        var signDataService = new InMemorySignDataService([]);
        var decorationDataService = new InMemoryDecorationDataService([]);
        var locationCatalogService = new InMemoryLocationCatalogService(locations);
        var service = new SeedDataService(signDataService, decorationDataService, locationCatalogService);

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

    private sealed class InMemorySignDataService : ISignDataService
    {
        private readonly IReadOnlyList<SignEntry> _entries;

        public InMemorySignDataService(IReadOnlyList<SignEntry> entries)
        {
            _entries = entries;
        }

        public void SetEntries(IReadOnlyList<SignEntry> entries)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<SignEntry> GetAllEntries()
            => _entries;

        public IReadOnlyList<SignEntry> GetEntriesByMap(int mapId)
            => [.. _entries.Where(entry => entry.MapId == mapId)];
    }

    private sealed class InMemoryDecorationDataService : IDecorationDataService
    {
        private readonly IReadOnlyList<DecorationEntry> _entries;

        public InMemoryDecorationDataService(IReadOnlyList<DecorationEntry> entries)
        {
            _entries = entries;
        }

        public void SetEntries(IReadOnlyList<DecorationEntry> entries)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<DecorationEntry> GetAllEntries()
            => _entries;

        public IReadOnlyList<DecorationEntry> GetEntriesByMap(int mapId)
            => [.. _entries.Where(entry => entry.MapId == mapId)];
    }

    private sealed class InMemoryLocationCatalogService : ILocationCatalogService
    {
        private readonly IReadOnlyList<WorldLocationEntry> _entries;

        public InMemoryLocationCatalogService(IReadOnlyList<WorldLocationEntry> entries)
        {
            _entries = entries;
        }

        public void SetLocations(IReadOnlyList<WorldLocationEntry> locations)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<WorldLocationEntry> GetAllLocations()
            => _entries;
    }
}
