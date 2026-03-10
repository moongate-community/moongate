using SixLabors.ImageSharp;

namespace Moongate.UO.Data.Interfaces.Maps;

/// <summary>
/// Generates radar-color PNG images for UO maps.
/// </summary>
public interface IMapImageService
{
    /// <summary>
    /// Renders the map with the given index as a radar-color image.
    /// Returns null if the map does not exist or map files are unavailable.
    /// </summary>
    Image? GetMapImage(int mapId);
}
