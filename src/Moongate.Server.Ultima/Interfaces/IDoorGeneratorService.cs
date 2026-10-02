using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Finds where the towns need doors by reading the door frames of the map's statics, as ModernUO's
///     <c>DoorGenerator</c>: a door between two facing frames two cells apart, a double door when they are three apart.
/// </summary>
public interface IDoorGeneratorService
{
    /// <summary>
    ///     Gets the areas of <paramref name="map" /> to scan, cut into pieces small enough for one game-loop work item
    ///     each; empty when the map is not loaded or has no door regions (ModernUO scans Trammel, Felucca, Ilshenar and
    ///     Malas).
    /// </summary>
    IReadOnlyList<Rectangle2D> ChunksOf(MapType map);

    /// <summary>
    ///     Gets the doors of the frames inside <paramref name="chunk" />; the opposite frame and the door may lie outside
    ///     it. A doorway that is walled up, by the map or by an item on the ground, or has no floor gets none, and a double door only when both halves fit. Call it
    ///     from the game loop: it reads the map.
    /// </summary>
    IReadOnlyList<GeneratedDoor> Scan(MapType map, Rectangle2D chunk);
}
