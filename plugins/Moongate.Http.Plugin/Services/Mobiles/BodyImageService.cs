using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Interfaces.Ultima;
using SquidStd.Core.Directories;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Caches body frames as PNG files on disk, the way <c>ItemImageService</c> caches art: File.Exists is
/// the whole hit path, the decode goes through the Ultima read gate, and the write is atomic.
/// </summary>
public sealed class BodyImageService : IBodyImageService
{
    private const string CacheDirectory = "cache/images/bodies";

    private readonly IAnimationCatalog _catalog;
    private readonly IUltimaReadGate _gate;
    private readonly string _cachePath;

    public BodyImageService(IAnimationCatalog catalog, DirectoriesConfig directories, IUltimaReadGate gate)
    {
        _catalog = catalog;
        _gate = gate;
        _cachePath = directories.RegisterDirectory(CacheDirectory);
    }

    public bool IsReady => _catalog.IsReady;

    public async Task<string?> GetOrCreateAsync(int body, int hue, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_cachePath, FileName(body, hue));

        if (File.Exists(path))
        {
            return path;
        }

        var frame = await _gate.ReadAsync(
            () => File.Exists(path) ? null : _catalog.GetFrame(body, 0, 1, 0, hue),
            cancellationToken
        );

        if (frame is null)
        {
            return File.Exists(path) ? path : null;
        }

        using (frame)
        {
            PngWriter.WriteAtomically(path, frame.Bitmap);
        }

        return path;
    }

    internal static string FileName(int body, int hue)
        => hue == 0 ? $"{body}.png" : $"{body}_{hue}.png";
}
