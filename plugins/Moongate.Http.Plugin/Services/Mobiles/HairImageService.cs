using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Interfaces.Ultima;
using SquidStd.Core.Directories;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>Caches hair styles rendered over a reference body, keyed by style, hue, body and kind.</summary>
public sealed class HairImageService : IHairImageService
{
    private const string CacheDirectory = "cache/images/hair";

    private readonly IMobileFigureRenderer _renderer;
    private readonly IUltimaReadGate _gate;
    private readonly string _cachePath;

    public HairImageService(IMobileFigureRenderer renderer, DirectoriesConfig directories, IUltimaReadGate gate)
    {
        _renderer = renderer;
        _gate = gate;
        _cachePath = directories.RegisterDirectory(CacheDirectory);
    }

    public async Task<string?> GetOrCreateAsync(
        int style,
        int hue,
        int referenceBody,
        bool facial,
        CancellationToken cancellationToken = default
    )
    {
        var prefix = facial ? "f" : "h";
        var path = Path.Combine(_cachePath, $"{prefix}{style}_{hue}_{referenceBody}.png");

        if (File.Exists(path))
        {
            return path;
        }

        var figure = await _gate.ReadAsync(
            () => File.Exists(path)
                ? null
                : _renderer.Render(
                    new(
                        referenceBody,
                        0,
                        facial ? 0 : style,
                        facial ? 0 : hue,
                        facial ? style : 0,
                        facial ? hue : 0,
                        []
                    )
                ),
            cancellationToken
        );

        if (figure is null)
        {
            return File.Exists(path) ? path : null;
        }

        using (figure)
        {
            PngWriter.WriteAtomically(path, figure);
        }

        return path;
    }
}
