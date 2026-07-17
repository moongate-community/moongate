using Moongate.Http.Plugin.Interfaces;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Interfaces;
using Moongate.Ultima.Tiles;
using SquidStd.Core.Directories;

namespace Moongate.Http.Plugin.Services;

/// <summary>
/// Caches item art as PNG files on disk. The decode itself belongs to <see cref="IItemCatalog" />; what
/// this adds is the cache, and the serialisation that makes it safe to call from a web server at all.
/// </summary>
public sealed class ItemImageService : IItemImageService, IDisposable
{
    /// <summary>
    /// Under the runtime root, which .gitignore already excludes wholesale — so the cache needs no ignore
    /// rule of its own. RegisterDirectory creates the whole tree.
    /// </summary>
    private const string CacheDirectory = "cache/images/items";

    private readonly IItemCatalog _catalog;
    private readonly string _cachePath;

    // One gate for every decode, not one per item. What it protects is Art's process-wide state — an LRU
    // bitmap cache, plain Dictionary fields and a shared static scratch buffer — so two requests for
    // different items corrupt each other exactly as two for the same item would. Art.cs holds no lock of
    // its own, and nothing else in the server calls it, so this is the only thing standing between a
    // concurrent request and that state.
    private readonly SemaphoreSlim _decodeGate = new(1, 1);

    public ItemImageService(IItemCatalog catalog, DirectoriesConfig directories)
    {
        _catalog = catalog;
        _cachePath = directories.RegisterDirectory(CacheDirectory);
    }

    public bool IsReady
        => TileData.ItemTable is not null;

    public async Task<string?> GetOrCreateAsync(
        uint itemId,
        ushort hue,
        CancellationToken cancellationToken = default
    )
    {
        var path = Path.Combine(_cachePath, FileName(itemId, hue));

        // The hit path takes no lock and never touches Art: concurrent readers of a finished file are the
        // OS's problem, not ours. This is what keeps the gate off the common case.
        if (File.Exists(path))
        {
            return path;
        }

        await _decodeGate.WaitAsync(cancellationToken);

        try
        {
            // Another request may have produced it while this one waited on the gate.
            if (File.Exists(path))
            {
                return path;
            }

            using var png = _catalog.GetItemImage(itemId, hue);

            if (png is null)
            {
                return null;
            }

            await WriteAtomicallyAsync(path, png, cancellationToken);

            return path;
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    public async Task<IReadOnlyList<uint>> GetArtItemIdsAsync(CancellationToken cancellationToken = default)
    {
        await _decodeGate.WaitAsync(cancellationToken);

        try
        {
            var ids = new List<uint>();
            var max = Art.GetMaxItemId();

            for (var id = 0; id <= max; id++)
            {
                if (Art.IsValidStatic(id))
                {
                    ids.Add((uint)id);
                }
            }

            return ids;
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    /// <summary>
    /// Lowercase hex, zero-padded, so names sort and cannot collide. The hue goes in the name rather than
    /// a subdirectory: one flat directory keeps the hit path a single File.Exists.
    /// </summary>
    internal static string FileName(uint itemId, ushort hue)
        => hue == 0 ? $"0x{itemId:x4}.png" : $"0x{itemId:x4}_0x{hue:x4}.png";

    /// <summary>
    /// Writes to a temporary file in the same directory, then moves it into place. A reader must never be
    /// handed a half-written PNG, and a move within one directory is atomic on Linux and Windows alike —
    /// writing straight to the final path would not be.
    /// </summary>
    private static async Task WriteAtomicallyAsync(string path, Stream png, CancellationToken cancellationToken)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var file = File.Create(temporary))
            {
                await png.CopyToAsync(file, cancellationToken);
            }

            File.Move(temporary, path, true);
        }
        catch
        {
            File.Delete(temporary);

            throw;
        }
    }

    public void Dispose()
        => _decodeGate.Dispose();
}
