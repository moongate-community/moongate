using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using SquidStd.Core.Directories;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Caches dressed template figures by template id. Hue specs resolve to their low end
/// (<see cref="LowestHue" />): the image must be the same on every request and every restart.
/// </summary>
public sealed class MobileTemplateImageService : IMobileTemplateImageService
{
    private const string CacheDirectory = "cache/images/mobile-templates";

    private readonly IMobileFigureRenderer _renderer;
    private readonly IMobileTemplateService _templates;
    private readonly IUltimaReadGate _gate;
    private readonly string _cachePath;

    public MobileTemplateImageService(
        IMobileFigureRenderer renderer,
        IMobileTemplateService templates,
        DirectoriesConfig directories,
        IUltimaReadGate gate
    )
    {
        _renderer = renderer;
        _templates = templates;
        _gate = gate;
        _cachePath = directories.RegisterDirectory(CacheDirectory);
    }

    public async Task<string?> GetOrCreateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var template = _templates.GetById(templateId);

        if (template is null)
        {
            return null;
        }

        var path = Path.Combine(_cachePath, $"{Sanitize(template.Id)}.png");

        if (File.Exists(path))
        {
            return path;
        }

        var appearance = template.Appearance;
        var request = new MobileFigureRequest(
            appearance.Body,
            LowestHue.Resolve(appearance.SkinHue),
            appearance.HairStyle,
            LowestHue.Resolve(appearance.HairHue),
            appearance.FacialHairStyle,
            LowestHue.Resolve(appearance.FacialHairHue),
            [.. template.Equipment.Select(worn => new MobileFigureEquipment(worn.Item, LowestHue.Resolve(worn.Hue)))]
        );

        var figure = await _gate.ReadAsync(
            () => File.Exists(path) ? null : _renderer.Render(request),
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

    private static string Sanitize(string id)
        => string.Concat(id.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
}
