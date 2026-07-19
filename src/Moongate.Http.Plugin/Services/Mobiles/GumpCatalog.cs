using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Imaging;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// The real catalog over Moongate.Ultima's gump statics. Gumps are cloned out of the internal decode
/// cache before hueing: callers dispose what they get, and the cache keeps what it owns.
/// </summary>
public sealed class GumpCatalog : IGumpCatalog
{
    // The male body paperdoll gump: present in every UO client, so a cheap, always-safe readiness
    // probe. Gumps.GetGump() itself is not null-safe before the client files are located (it derefs
    // the file index unconditionally past the bounds/cache checks), and Gumps.GetCount() falls back to
    // a nonzero constant even when nothing loaded — neither works as a readiness signal. IsValidIndex
    // is the one member that null-checks the file index first.
    private const int ReadinessProbeGumpId = 0x000C;

    public bool IsReady => Gumps.IsValidIndex(ReadinessProbeGumpId);

    public UltimaBitmap? GetGump(int gumpId, int hue, bool partialHue)
    {
        if (!Gumps.IsValidIndex(gumpId))
        {
            return null;
        }

        var source = Gumps.GetGump(gumpId);

        if (source is null)
        {
            return null;
        }

        var owned = source.Clone();

        // Same index math as the item art path: hue is the 1-based wire value, 0 means "no hue".
        var index = (hue & 0x3FFF) - 1;

        if (index >= 0)
        {
            Hues.GetHue(index).ApplyTo(owned, partialHue);
        }

        return owned;
    }
}
