using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Maps;

/// <summary>
/// The per-facet <see cref="MapDefinition" /> registry (bounds + default season), with values taken
/// from ModernUO's map-definitions. An unknown map id falls back to Felucca.
/// </summary>
public static class MapDefinitions
{
    private static readonly MapDefinition[] Definitions =
    [
        new(MapType.Felucca, 7168, 4096, SeasonType.Desolation),
        new(MapType.Trammel, 7168, 4096, SeasonType.Spring),
        new(MapType.Ilshenar, 2304, 1600, SeasonType.Summer),
        new(MapType.Malas, 2560, 2048, SeasonType.Summer),
        new(MapType.Tokuno, 1448, 1448, SeasonType.Summer),
        new(MapType.TerMur, 1280, 4096, SeasonType.Summer)
    ];

    /// <summary>Returns the definition for the facet, or Felucca's when <paramref name="mapId" /> is unknown.</summary>
    public static MapDefinition Get(int mapId)
        => mapId >= 0 && mapId < Definitions.Length ? Definitions[mapId] : Definitions[0];
}
