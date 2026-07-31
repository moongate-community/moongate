using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Data.Mobiles;

namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>
/// Images of a real character, wearing what they actually wear.
/// <para>
/// The cache is content-addressed: a file is named with the fingerprint of the appearance it
/// shows, so changing clothes produces a different file and there is nothing to invalidate. Two
/// characters dressed alike share one.
/// </para>
/// </summary>
public interface IMobileCharacterImageService
{
    /// <summary>The dressed figure, or null when the serial names no mobile or its body cannot be drawn.</summary>
    Task<MobileImage?> GetFigureAsync(Serial serial, CancellationToken cancellationToken = default);

    /// <summary>The paperdoll, or null when the serial names no mobile.</summary>
    Task<MobileImage?> GetPaperdollAsync(
        Serial serial,
        bool includeBackground,
        CancellationToken cancellationToken = default
    );
}
