using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for skills: how many points a character may have in all, and whether skills rise with use.
/// </summary>
public sealed class SkillsConfig
{
    private const int MaximumTotalCap = 100_000;
    private const int MaximumStat = IMobileStateService.MaxValue;
    private const double MaximumGainMinutes = 1440;

    /// <summary>
    ///     Gets or sets the points a character may have in all its skills together; 700 is the classic cap, seven
    ///     skills at 100.
    /// </summary>
    public int TotalCap { get; set; } = 700;

    /// <summary>
    ///     Gets or sets whether using a skill can raise it. Off, skills change only by a game master or a script.
    /// </summary>
    public bool GainEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the highest value a player's strength, dexterity or intelligence can reach by use; 100 is the
    ///     classic maximum.
    /// </summary>
    public int StatMax { get; set; } = 100;

    /// <summary>
    ///     Gets or sets the points the three stats of a player add up to at most; 225 is the classic cap. Over it, a
    ///     stat rises only when another one, locked down, gives a point.
    /// </summary>
    public int StatCap { get; set; } = 225;

    /// <summary>
    ///     Gets or sets the minutes a stat waits after it was tried before it is tried again; 10 as the classic
    ///     ModernUO. 0 tries it at every successful skill.
    /// </summary>
    public double StatGainMinutes { get; set; } = 10;

    /// <summary>
    ///     Validates the section before server services begin startup: a cap from 1 point up, a stat maximum from 1
    ///     and a stat cap from 30 (three stats of 10, the least a player has).
    /// </summary>
    public void Validate()
    {
        if (TotalCap is < 1 or > MaximumTotalCap)
        {
            throw new InvalidOperationException(
                $"ultima.skills.total_cap must be from 1 to {MaximumTotalCap}, found {TotalCap}."
            );
        }

        if (StatMax is < 1 or > MaximumStat)
        {
            throw new InvalidOperationException($"ultima.skills.stat_max must be from 1 to {MaximumStat}, found {StatMax}.");
        }

        if (StatCap is < 30 or > MaximumTotalCap)
        {
            throw new InvalidOperationException($"ultima.skills.stat_cap must be from 30 to {MaximumTotalCap}, found {StatCap}.");
        }

        if (StatGainMinutes is < 0 or > MaximumGainMinutes || double.IsNaN(StatGainMinutes))
        {
            throw new InvalidOperationException(
                $"ultima.skills.stat_gain_minutes must be from 0 to {MaximumGainMinutes}, found {StatGainMinutes}."
            );
        }
    }
}
