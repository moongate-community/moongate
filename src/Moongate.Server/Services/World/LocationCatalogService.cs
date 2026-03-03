using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory location catalog populated at startup by file loaders.
/// </summary>
public class LocationCatalogService : ILocationCatalogService
{
    private readonly object _sync = new();
    private List<WorldLocationEntry> _locations = [];

    public void SetLocations(IReadOnlyList<WorldLocationEntry> locations)
    {
        lock (_sync)
        {
            _locations = [..locations];
        }
    }

    public IReadOnlyList<WorldLocationEntry> GetAllLocations()
    {
        lock (_sync)
        {
            return [.._locations];
        }
    }
}
