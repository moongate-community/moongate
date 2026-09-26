using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Skills;

/// <summary>
///     One skill of <c>skills.toml</c>: its protocol id, its names and how it makes the stats grow.
/// </summary>
public class SkillContent
{
    /// <summary>
    ///     The skill; its value is the id the client uses.
    /// </summary>
    public SkillType Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    ///     The title of a character whose best skill is this one.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    ///     The name the profession files use for this skill, which can differ from <see cref="Name" />.
    /// </summary>
    public string ProfessionName { get; set; }

    public StatType PrimaryStat { get; set; }

    public StatType SecondaryStat { get; set; }

    /// <summary>
    ///     The chance, in percent, that a gain of this skill also raises strength.
    /// </summary>
    public double StrScale { get; set; }

    /// <summary>
    ///     The chance, in percent, that a gain of this skill also raises dexterity.
    /// </summary>
    public double DexScale { get; set; }

    /// <summary>
    ///     The chance, in percent, that a gain of this skill also raises intelligence.
    /// </summary>
    public double IntScale { get; set; }

    /// <summary>
    ///     How much a gain of this skill favours strength when a stat rises.
    /// </summary>
    public double StrGain { get; set; }

    /// <summary>
    ///     How much a gain of this skill favours dexterity when a stat rises.
    /// </summary>
    public double DexGain { get; set; }

    /// <summary>
    ///     How much a gain of this skill favours intelligence when a stat rises.
    /// </summary>
    public double IntGain { get; set; }

    /// <summary>
    ///     How fast the skill itself rises; 1.0 is normal.
    /// </summary>
    public double GainFactor { get; set; }

    /// <summary>
    ///     Gets the total chance, in percent, that a gain of this skill raises a stat.
    /// </summary>
    public double StatTotal => StrScale + DexScale + IntScale;
}
