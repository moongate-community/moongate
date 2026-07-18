using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Ultima.Imaging;

namespace Moongate.Tests.Support;

/// <summary>Gumps without client files: tests declare which (gumpId, hue) pairs decode and to what size.</summary>
public sealed class FakeGumpCatalog : IGumpCatalog
{
    /// <summary>(gumpId, hue) → (width, height). partialHue is accepted but does not affect the fake.</summary>
    public Dictionary<(int GumpId, int Hue), (int W, int H)> Gumps { get; } = new();

    public bool IsReady { get; set; } = true;

    public UltimaBitmap? GetGump(int gumpId, int hue, bool partialHue)
        => Gumps.TryGetValue((gumpId, hue), out var size) ? new UltimaBitmap(size.W, size.H) : null;
}
