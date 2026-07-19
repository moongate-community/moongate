using Moongate.Http.Plugin.Interfaces.Images;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Interfaces;
using Moongate.Ultima.Tiles;
using SquidStd.Core.Directories;

namespace Moongate.Http.Plugin.Services.Images;

/// <summary>
/// Caches item art as PNG files on disk. The decode itself belongs to <see cref="IItemCatalog" />; what
/// this adds is the cache, and the serialisation that makes it safe to call from a web server at all.
/// </summary>
public sealed class ItemImageService : IItemImageService
{
    /// <summary>
    /// Under the runtime root, which .gitignore already excludes wholesale — so the cache needs no ignore
    /// rule of its own. RegisterDirectory creates the whole tree.
    /// </summary>
    private const string CacheDirectory = "cache/images/items";

    private readonly IItemCatalog _catalog;
    private readonly IUltimaReadGate _gate;
    private readonly string _cachePath;

    public ItemImageService(IItemCatalog catalog, DirectoriesConfig directories, IUltimaReadGate gate)
    {
        _catalog = catalog;
        _gate = gate;
        _cachePath = directories.RegisterDirectory(CacheDirectory);
    }

    public bool IsReady => TileData.ItemTable is not null;

    public async Task<IReadOnlyList<uint>> GetArtItemIdsAsync(CancellationToken cancellationToken = default)
        => await _gate.ReadAsync(
               () =>
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

                   return (IReadOnlyList<uint>)ids;
               },
               cancellationToken
           );

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

        // Re-checked inside the gate, because another request may have produced it while this one waited.
        // The decode is all that needs the gate — the write is ordinary file I/O and is done outside it.
        var png = await _gate.ReadAsync(
                      () => File.Exists(path) ? null : _catalog.GetItemImage(itemId, hue),
                      cancellationToken
                  );

        // Null means two different things here: another request won the race, or the item has no art.
        // The file is what tells them apart — returning null blindly would turn a cache race into a 404.
        if (png is null)
        {
            return File.Exists(path) ? path : null;
        }

        using (png)
        {
            await WriteAtomicallyAsync(path, png, cancellationToken);
        }

        return path;
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
}
