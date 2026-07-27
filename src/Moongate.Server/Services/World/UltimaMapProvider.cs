using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Maps;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// Ultima's own facets, by <see cref="MapType" />. The sizes are Ultima's, not MapDefinitions': Ultima
/// hardcodes Felucca at 6144×4096 while MapDefinitions says 7168, and callers can only query what
/// Ultima's Map object describes. On a shard with post-ML client files this reports the older extent.
/// </summary>
public sealed class UltimaMapProvider : IUltimaMapProvider
{
    public IReadOnlyList<MapType> Facets { get; } =
    [
        MapType.Felucca,
        MapType.Trammel,
        MapType.Ilshenar,
        MapType.Malas,
        MapType.Tokuno,
        MapType.TerMur
    ];

    public Map? Get(MapType facet)
        => facet switch
        {
            MapType.Felucca  => Map.Felucca,
            MapType.Trammel  => Map.Trammel,
            MapType.Ilshenar => Map.Ilshenar,
            MapType.Malas    => Map.Malas,
            MapType.Tokuno   => Map.Tokuno,
            MapType.TerMur   => Map.TerMur,
            _                => null
        };
}
