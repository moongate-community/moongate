using Moongate.Ultima.Maps;
using Moongate.UO.Data.Types;

namespace Moongate.Http.Plugin.Interfaces;

/// <summary>
/// The facets the shard's client files describe. Exists as an interface because Ultima's facets are static
/// fields with hardcoded sizes — Felucca is 6144×4096 — and a test that had to render one would be
/// rendering 384 native tiles to prove a pyramid composes.
/// </summary>
public interface IUltimaMapProvider
{
    /// <summary>Every facet the API serves.</summary>
    IReadOnlyList<MapType> Facets { get; }

    /// <summary>The facet's map, or null when it is not one this provider serves.</summary>
    Map? Get(MapType facet);
}
