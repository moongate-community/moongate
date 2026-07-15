namespace Moongate.UO.Data.Maps;

/// <summary>
/// Wire dimensions of each UO map facet, as the client expects them in the login confirmation
/// (0x1B) packet. These are the full coordinate bounds of the map (like ModernUO's hardcoded map
/// definitions), NOT the same thing as <c>Moongate.Ultima.Maps.Map.Width</c>: for Felucca/Trammel
/// the protocol bound is 7168 (the whole addressable map, including the Lost Lands strip) whereas
/// the tile renderer uses 6144 (the playable continent). The other facets coincide. An unknown id
/// falls back to Felucca.
/// </summary>
public static class MapDimensions
{
    private static readonly (int Width, int Height)[] Sizes =
    [
        (7168, 4096), // 0 Felucca
        (7168, 4096), // 1 Trammel
        (2304, 1600), // 2 Ilshenar
        (2560, 2048), // 3 Malas
        (1448, 1448), // 4 Tokuno
        (1280, 4096)  // 5 TerMur
    ];

    /// <summary>Returns the (width, height) of the facet, or Felucca's when <paramref name="mapId" /> is unknown.</summary>
    public static (int Width, int Height) Get(int mapId)
        => mapId >= 0 && mapId < Sizes.Length ? Sizes[mapId] : Sizes[0];
}
