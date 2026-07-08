using Moongate.UO.Data.Teleporters;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces;

/// <summary>In-memory registry of teleporters.</summary>
public interface ITeleporterService
{
    /// <summary>All registered teleporters in load order.</summary>
    IReadOnlyList<TeleporterDefinition> All { get; }

    /// <summary>Number of registered teleporters.</summary>
    int Count { get; }

    /// <summary>Adds a teleporter to the registry.</summary>
    void Register(TeleporterDefinition teleporter);

    /// <summary>Returns the teleporters whose source is on the given map.</summary>
    IReadOnlyList<TeleporterDefinition> ForMap(MapType map);
}
