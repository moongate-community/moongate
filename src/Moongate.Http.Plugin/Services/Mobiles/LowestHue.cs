using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Resolves a template hue spec deterministically: a range yields its low end. Figure images must be
/// cacheable by template id, so the random the game uses at spawn time has no place here.
/// </summary>
public static class LowestHue
{
    private static readonly Random Low = new LowRandom();

    public static ushort Resolve(string? spec)
        => HueSpec.Resolve(spec, Low);

    private sealed class LowRandom : Random
    {
        public override int Next(int minValue, int maxValue)
            => minValue;
    }
}
