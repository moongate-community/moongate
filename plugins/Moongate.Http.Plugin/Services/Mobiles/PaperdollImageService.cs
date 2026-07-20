using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Caches paperdolls by template id (and background flag). Hue specs resolve to their low end and
/// Random gender resolves to Male (<see cref="ResolveGender" />): the image must be the same on every
/// request and every restart, the same determinism <see cref="LowestHue" /> gives hue specs.
/// </summary>
public sealed class PaperdollImageService : IPaperdollImageService
{
    private const string CacheDirectory = "cache/images/paperdolls";

    private readonly IPaperdollRenderer _renderer;
    private readonly IMobileTemplateService _templates;
    private readonly IUltimaReadGate _gate;
    private readonly string _cachePath;

    public PaperdollImageService(
        IPaperdollRenderer renderer,
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

    public async Task<string?> GetOrCreateAsync(
        string templateId,
        bool includeBackground,
        CancellationToken cancellationToken = default
    )
    {
        var template = _templates.GetById(templateId);

        if (template is null)
        {
            return null;
        }

        var suffix = includeBackground ? string.Empty : "_nobg";
        var path = Path.Combine(_cachePath, $"{Sanitize(template.Id)}{suffix}.png");

        if (File.Exists(path))
        {
            return path;
        }

        var appearance = template.Appearance;
        var request = new PaperdollRenderRequest(
            ResolveGender(template.Gender),
            includeBackground,
            LowestHue.Resolve(appearance.SkinHue),
            appearance.HairStyle,
            LowestHue.Resolve(appearance.HairHue),
            appearance.FacialHairStyle,
            LowestHue.Resolve(appearance.FacialHairHue),
            [.. template.Equipment.Select(worn => new MobileFigureEquipment(worn.Item, LowestHue.Resolve(worn.Hue)))]
        );

        var doll = await _gate.ReadAsync(
                       () => File.Exists(path) ? null : _renderer.Render(request),
                       cancellationToken
                   );

        if (doll is null)
        {
            return File.Exists(path) ? path : null;
        }

        using (doll)
        {
            PngWriter.WriteAtomically(path, doll);
        }

        return path;
    }

    private static GenderType ResolveGender(MobileTemplateGenderType gender)
        => gender == MobileTemplateGenderType.Female ? GenderType.Female : GenderType.Male;

    private static string Sanitize(string id)
        => string.Concat(id.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
}
