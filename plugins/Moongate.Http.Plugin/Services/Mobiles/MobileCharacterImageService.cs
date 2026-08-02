using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Ultima.Imaging;
using SquidStd.Core.Directories;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Images of a real character, cached by what they look like rather than by who they are.
/// <para>
/// The file is named with the appearance fingerprint, so a change of clothes writes a new file and
/// leaves the old one alone — there is no invalidation to get wrong, and no path by which a
/// picture can be confidently out of date. Two characters dressed alike share a file.
/// </para>
/// <para>
/// Unlike the template services there is no <c>LowestHue</c> resolution here: a template carries
/// hue *specs*, a real mobile carries hues that are already resolved.
/// </para>
/// </summary>
public sealed class MobileCharacterImageService : IMobileCharacterImageService
{
    private const string CacheDirectory = "cache/images/characters";

    private readonly IMobileFigureRenderer _figures;
    private readonly IPaperdollRenderer _paperdolls;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IItemService _items;
    private readonly IUltimaReadGate _gate;
    private readonly string _cachePath;

    public MobileCharacterImageService(
        IMobileFigureRenderer figures,
        IPaperdollRenderer paperdolls,
        IPersistenceService persistenceService,
        IItemService items,
        DirectoriesConfig directories,
        IUltimaReadGate gate
    )
    {
        _figures = figures;
        _paperdolls = paperdolls;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _items = items;
        _gate = gate;
        _cachePath = directories.RegisterDirectory(CacheDirectory);
    }

    public async Task<MobileImage?> GetFigureAsync(Serial serial, CancellationToken cancellationToken = default)
    {
        if (_mobiles.GetById(serial) is not { } mobile)
        {
            return null;
        }

        var equipped = _items.GetEquipped(mobile).ToList();
        var hash = MobileAppearanceHash.Of(mobile, equipped);
        var path = Path.Combine(_cachePath, $"{hash}.png");

        if (Cached(path))
        {
            return new(path, hash);
        }

        var request = new MobileFigureRequest(
            mobile.Body,
            mobile.SkinHue.Value,
            mobile.HairStyle,
            mobile.HairHue.Value,
            mobile.FacialHairStyle,
            mobile.FacialHairHue.Value,
            [
                .. equipped.Where(worn => worn.EquippedLayer is not null)
                           .Select(worn => new MobileFigureEquipment(worn.TemplateId, worn.Hue.Value))
            ]
        );

        return await RenderAsync(() => _figures.Render(request), path, hash, cancellationToken);
    }

    public async Task<MobileImage?> GetPaperdollAsync(
        Serial serial,
        bool includeBackground,
        CancellationToken cancellationToken = default
    )
    {
        if (_mobiles.GetById(serial) is not { } mobile)
        {
            return null;
        }

        var equipped = _items.GetEquipped(mobile).ToList();

        // The background is part of the picture, so it is part of the key.
        var hash = MobileAppearanceHash.Of(mobile, equipped) + (includeBackground ? "_doll" : "_doll_nobg");
        var path = Path.Combine(_cachePath, $"{hash}.png");

        if (Cached(path))
        {
            return new(path, hash);
        }

        var request = new PaperdollRenderRequest(
            mobile.Gender,
            includeBackground,
            mobile.SkinHue.Value,
            mobile.HairStyle,
            mobile.HairHue.Value,
            mobile.FacialHairStyle,
            mobile.FacialHairHue.Value,
            [
                .. equipped.Where(worn => worn.EquippedLayer is not null)
                           .Select(worn => new MobileFigureEquipment(worn.TemplateId, worn.Hue.Value))
            ]
        );

        return await RenderAsync(() => _paperdolls.Render(request), path, hash, cancellationToken);
    }

    /// <summary>
    /// A hit, and the touch that keeps it alive. The sweep reads last-write time — deliberately, since
    /// last-*access* time is disabled or coarsened on many mounts and a picture in daily use could
    /// otherwise look untouched for a week.
    /// </summary>
    private static bool Cached(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (IOException)
        {
            // Failing to touch it only risks an early sweep, and the picture would be re-rendered.
        }

        return true;
    }

    private async Task<MobileImage?> RenderAsync(
        Func<UltimaBitmap?> render,
        string path,
        string hash,
        CancellationToken cancellationToken
    )
    {
        var image = await _gate.ReadAsync(() => File.Exists(path) ? null : render(), cancellationToken);

        if (image is null)
        {
            return File.Exists(path) ? new MobileImage(path, hash) : null;
        }

        using (image)
        {
            PngWriter.WriteAtomically(path, image);
        }

        return new(path, hash);
    }
}
