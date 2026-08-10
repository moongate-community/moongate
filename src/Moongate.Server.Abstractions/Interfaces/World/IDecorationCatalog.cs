using Moongate.UO.Data.World;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// The world's decoration, as the tracked YAML under Assets describes it: every static object the
/// world should hold, before any of it has been placed.
/// </summary>
public interface IDecorationCatalog
{
    /// <summary>Every object across every facet loaded so far.</summary>
    IReadOnlyList<DecorationPlacement> All { get; }

    /// <summary>Expands one facet's groups and adds them. Called once per map file at startup.</summary>
    /// <param name="mapId">The facet the groups belong to.</param>
    /// <param name="groups">The groups as the YAML holds them.</param>
    void Add(int mapId, IEnumerable<DecorationGroup> groups);
}
