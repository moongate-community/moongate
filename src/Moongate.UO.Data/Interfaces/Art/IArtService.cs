using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.UO.Data.Interfaces.Art;

/// <summary>
/// Provides access to item art bitmaps from Ultima Online art files.
/// </summary>
public interface IArtService
{
    /// <summary>
    /// Loads an item-art bitmap by item id.
    /// </summary>
    /// <param name="itemId">Item graphic id.</param>
    /// <param name="clone">
    /// When <see langword="true" />, returns a detached copy of the cached bitmap.
    /// </param>
    /// <returns>The decoded image when present; otherwise <see langword="null" />.</returns>
    Image<Rgba32>? GetArt(int itemId, bool clone = true);

    /// <summary>
    /// Checks whether item art exists for the provided item id.
    /// </summary>
    /// <param name="itemId">Item graphic id.</param>
    /// <returns><see langword="true" /> when art exists; otherwise <see langword="false" />.</returns>
    bool IsValidArt(int itemId);
}
