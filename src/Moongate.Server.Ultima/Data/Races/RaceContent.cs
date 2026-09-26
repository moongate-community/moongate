using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Races;

/// <summary>
///     One race of <c>races.toml</c>, with what each of its genders gets.
/// </summary>
public class RaceContent
{
    /// <summary>
    ///     The bits of a hue that select the color; the higher ones are flags, such as the partial hue flag.
    /// </summary>
    private const int HueMask = 0x3FFF;

    public RaceType Race { get; set; }

    public string Name { get; set; }

    /// <summary>
    ///     The allowed skin hues, as values or ranges; empty allows any hue.
    /// </summary>
    public List<HueSpec> SkinHues { get; set; } = [];

    /// <summary>
    ///     The allowed hair and beard hues, as values or ranges; empty allows any hue.
    /// </summary>
    public List<HueSpec> HairHues { get; set; } = [];

    public RaceGenderContent Male { get; set; }

    public RaceGenderContent Female { get; set; }

    /// <summary>
    ///     Gets what <paramref name="gender" /> of this race gets.
    /// </summary>
    public RaceGenderContent For(GenderType gender)
    {
        return gender == GenderType.Female ? Female : Male;
    }

    /// <summary>
    ///     Returns <paramref name="hue" /> when it is an allowed skin hue, otherwise the nearest allowed one. Only the
    ///     color bits are compared and returned, without flags such as the partial hue flag.
    /// </summary>
    public Hue ClipSkinHue(Hue hue)
    {
        return Clip(SkinHues, hue);
    }

    /// <summary>
    ///     Returns <paramref name="hue" /> when it is an allowed hair hue, otherwise the nearest allowed one.
    /// </summary>
    public Hue ClipHairHue(Hue hue)
    {
        return Clip(HairHues, hue);
    }

    private static Hue Clip(List<HueSpec> allowed, Hue hue)
    {
        var value = hue.Value & HueMask;

        if (allowed.Count == 0)
        {
            return new((ushort)value);
        }

        var nearest = allowed[0].Min;

        foreach (var spec in allowed)
        {
            var candidate = Math.Clamp(value, spec.Min, spec.Max);

            if (candidate == value)
            {
                return new((ushort)value);
            }

            if (Math.Abs(candidate - value) < Math.Abs(nearest - value))
            {
                nearest = candidate;
            }
        }

        return new((ushort)nearest);
    }
}
