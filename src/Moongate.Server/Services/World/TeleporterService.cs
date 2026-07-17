using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Teleporters;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

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

    public IReadOnlyList<TeleporterDefinition> ForMap(MapType map)
        => [.. _teleporters.Where(teleporter => teleporter.Src.Map == map)];

    public void Register(TeleporterDefinition teleporter)
        => _teleporters.Add(teleporter);
}
