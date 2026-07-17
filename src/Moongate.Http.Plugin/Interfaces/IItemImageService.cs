namespace Moongate.Http.Plugin.Interfaces;

/// <summary>
/// Item art as PNG files, decoded from the UO client files on first request and cached on disk.
/// </summary>
public interface IItemImageService
{
    /// <summary>
    /// False when the UO client files are not loaded, and no image can be produced at all. Keeps two
    /// different failures apart: an item that has no art, and a shard whose UltimaDirectory is wrong.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// The path of the cached PNG for this item and hue, decoding and caching it on first request. Null
    /// when the item has no art.
    /// </summary>
    Task<string?> GetOrCreateAsync(uint itemId, ushort hue, CancellationToken cancellationToken = default);

    /// <summary>Every item id the client files hold static art for.</summary>
    Task<IReadOnlyList<uint>> GetArtItemIdsAsync(CancellationToken cancellationToken = default);
}
