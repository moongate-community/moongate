using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory storage for sign data loaded at startup.
/// </summary>
public class SignDataService : ISignDataService
{
    private readonly object _sync = new();
    private List<SignEntry> _entries = [];
    private Dictionary<int, List<SignEntry>> _entriesByMap = [];

    public IReadOnlyList<SignEntry> GetAllEntries()
    {
        lock (_sync)
        {
            return [.._entries];
        }
    }

    public IReadOnlyList<SignEntry> GetEntriesByMap(int mapId)
    {
        lock (_sync)
        {
            if (!_entriesByMap.TryGetValue(mapId, out var entries))
            {
                return [];
            }

            return [..entries];
        }
    }

    public void SetEntries(IReadOnlyList<SignEntry> entries)
    {
        lock (_sync)
        {
            _entries = [..entries];
            _entriesByMap = entries.GroupBy(static entry => entry.MapId)
                                   .ToDictionary(
                                       static grouping => grouping.Key,
                                       static grouping => grouping.ToList()
                                   );
        }
    }
}
