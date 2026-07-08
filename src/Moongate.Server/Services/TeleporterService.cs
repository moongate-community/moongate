using Moongate.Server.Interfaces;
using Moongate.UO.Data.Teleporters;

namespace Moongate.Server.Services;

/// <summary>
/// In-memory registry of teleporters. Populated at startup by
/// <see cref="Moongate.Server.Loaders.TeleportersLoader" />. Teleporters are not uniquely keyed, so
/// they are held as an ordered list and queried by source map.
/// </summary>
public sealed class TeleporterService : ITeleporterService
{
    private readonly List<TeleporterDefinition> _teleporters = [];

    public IReadOnlyList<TeleporterDefinition> All => _teleporters;

    public int Count => _teleporters.Count;

    public void Register(TeleporterDefinition teleporter)
    {
        _teleporters.Add(teleporter);
    }

    public IReadOnlyList<TeleporterDefinition> ForMap(string map)
    {
        return [.. _teleporters.Where(teleporter => string.Equals(teleporter.Src.Map, map, StringComparison.OrdinalIgnoreCase))];
    }
}
