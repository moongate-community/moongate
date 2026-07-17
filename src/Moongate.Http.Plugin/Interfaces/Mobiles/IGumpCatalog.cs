using Moongate.Ultima.Imaging;

namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>
/// Decoded, hued gump art behind a seam the tests can fake: the real implementation reads
/// Moongate.Ultima's process-wide statics, which tests do not have.
/// </summary>
public interface IGumpCatalog
{
    /// <summary>True once the client files behind the gumps are loaded.</summary>
    bool IsReady { get; }

    /// <summary>A decoded, hued gump the caller owns, or null when the index has no art.</summary>
    UltimaBitmap? GetGump(int gumpId, int hue, bool partialHue);
}
