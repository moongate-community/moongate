using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory store for decoration entries loaded at startup.
/// </summary>
public class DecorationDataService : IDecorationDataService
{
    private readonly object _sync = new();
    private List<DecorationEntry> _entries = [];
    private Dictionary<int, List<DecorationEntry>> _entriesByMap = [];

    public IReadOnlyList<DecorationEntry> GetAllEntries()
    {
        lock (_sync)
        {
            return [.._entries];
        }
    }

    public IReadOnlyList<DecorationEntry> GetEntriesByMap(int mapId)
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

    public void SetEntries(IReadOnlyList<DecorationEntry> entries)
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
