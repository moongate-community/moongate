using System.Globalization;
using System.Text;
using Moongate.Persistence.Entities;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// A stable fingerprint of everything visible about a mobile. It is the cache file's name and the
/// response's ETag, which is what makes a change of clothes a new file rather than a cache to
/// invalidate.
/// </summary>
public static class MobileAppearanceHash
{
    /// <summary>
    /// FNV-1a over the appearance, with worn items ordered by layer so an outfit hashes the same
    /// however the store hands it back.
    /// <para>
    /// Items are fingerprinted by <c>TemplateId</c> and not <c>ItemId</c>, because the template id is
    /// what the renderer is given: hashing the art id would sign something the picture does not
    /// depend on while leaving out something it does.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>string.GetHashCode</c>, which is randomised per process: a cache key that
    /// changed between restarts would silently re-render everything after every boot.
    /// </remarks>
    public static string Of(MobileEntity mobile, IReadOnlyList<ItemEntity> equipped)
    {
        var builder = new StringBuilder();

        builder.Append(
            CultureInfo.InvariantCulture,
            $"{mobile.Body}|{mobile.SkinHue.Value}|{mobile.HairStyle}|{mobile.HairHue.Value}|{mobile.FacialHairStyle}|{mobile.FacialHairHue.Value}"
        );

        // Anything not on a layer is carried rather than worn, and cannot be seen.
        foreach (var item in equipped.Where(worn => worn.EquippedLayer is not null)
                                     .OrderBy(worn => (int)worn.EquippedLayer!.Value))
        {
            builder.Append(
                CultureInfo.InvariantCulture,
                $"|{(int)item.EquippedLayer!.Value}:{item.TemplateId}:{item.Hue.Value}"
            );
        }

        return Fnv(builder.ToString());
    }

    private static string Fnv(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        var hash = offset;

        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash = (hash ^ b) * prime;
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }
}
