using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Maps;

/// <summary>
/// Per-facet metadata Moongate needs at the protocol level: which facet it is, the coordinate bounds
/// sent in the login confirmation (0x1B), and the default season sent in the seasonal information
/// (0xBC) packet. The server-side equivalent of ModernUO's map-definitions, kept separate from the
/// tile renderer's <c>Moongate.Ultima.Maps.Map</c> (whose Felucca/Trammel width is the 6144 playable
/// continent).
/// </summary>
public readonly record struct MapDefinition(MapType Map, int Width, int Height, SeasonType Season);
