using Moongate.Ultima.Graphics;
using Moongate.Ultima.Helpers;

namespace Moongate.Http.Plugin.Data.Api.Graphics;

/// <summary>One row of the hue catalogue: what to call a hue, and what it looks like.</summary>
public sealed record HueSummary(int Value, string Name, string Hex, IReadOnlyList<string> Gradient)
{
    /// <summary>How many of the hue's 32 shades the gradient carries, evenly spaced across the ramp.</summary>
    public const int GradientSteps = 8;

    /// <summary>
    /// Projects one entry of the hue table into its catalogue row. <paramref name="value" /> is the hue
    /// as the client and the packets use it — 1-based, where 0 means unhued — while the table itself is
    /// indexed from 0.
    /// </summary>
    public static HueSummary From(int value, Hue hue)
    {
        var colors = hue.Colors;
        var gradient = new List<string>(GradientSteps);

        for (var step = 0; step < GradientSteps && colors.Length > 0; step++)
        {
            gradient.Add(ToHex(colors[step * (colors.Length - 1) / Math.Max(GradientSteps - 1, 1)]));
        }

        // The middle of the ramp is what a swatch should show: the ends are the near-black and the
        // near-white every hue shares, and neither tells two hues apart.
        var representative = colors.Length > 0 ? ToHex(colors[colors.Length / 2]) : "#000000";

        return new(value, string.IsNullOrWhiteSpace(hue.Name) ? $"Hue {value}" : hue.Name.Trim(), representative, gradient);
    }

    /// <summary>
    /// A hue's colours are RGB555 with the top bit clear, so the components are expanded to 8 bits
    /// rather than masked out of a 16-bit ARGB word.
    /// </summary>
    public static string ToHex(ushort color)
        => $"#{HueHelpers.HueToColorR(color):X2}{HueHelpers.HueToColorG(color):X2}{HueHelpers.HueToColorB(color):X2}";
}
