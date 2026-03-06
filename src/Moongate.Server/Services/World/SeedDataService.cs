using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;

namespace Moongate.Server.Services.World;

/// <summary>
/// Aggregates access to seed datasets through dedicated domain services.
/// </summary>
public class SeedDataService : ISeedDataService
{
    private readonly ISignDataService _signDataService;
    private readonly IDecorationDataService _decorationDataService;
    private readonly IDoorDataService _doorDataService;
    private readonly ILocationCatalogService _locationCatalogService;

    public SeedDataService(
        ISignDataService signDataService,
        IDecorationDataService decorationDataService,
        IDoorDataService doorDataService,
        ILocationCatalogService locationCatalogService
    )
    {
        _signDataService = signDataService;
        _decorationDataService = decorationDataService;
        _doorDataService = doorDataService;
        _locationCatalogService = locationCatalogService;
    }

    public IReadOnlyList<DecorationEntry> GetDecorationsByMap(int mapId)
        => _decorationDataService.GetEntriesByMap(mapId);

    public IReadOnlyList<DoorComponentEntry> GetDoors()
        => _doorDataService.GetAllEntries();

    public IReadOnlyList<WorldLocationEntry> GetLocations()
        => _locationCatalogService.GetAllLocations();

    public IReadOnlyList<SignEntry> GetSignsByMap(int mapId)
        => _signDataService.GetEntriesByMap(mapId);
}
