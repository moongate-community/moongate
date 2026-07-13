using Moongate.UO.Data.Signs;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.World;

/// <summary>In-memory registry of world sign placements.</summary>
public interface ISignService
{
    /// <summary>All registered signs in load order.</summary>
    IReadOnlyList<SignEntry> All { get; }

    /// <summary>Number of registered signs.</summary>
    int Count { get; }

    /// <summary>Adds a sign to the registry.</summary>
    void Register(SignEntry sign);

    /// <summary>Returns the signs on the given map.</summary>
    IReadOnlyList<SignEntry> ForMap(MapType map);
}
