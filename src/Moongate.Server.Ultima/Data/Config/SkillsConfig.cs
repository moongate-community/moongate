namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for skills: how many points a character may have in all, and whether skills rise with use.
/// </summary>
public sealed class SkillsConfig
{
    private const int MaximumTotalCap = 100_000;

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
    ///     Validates the section before server services begin startup: a cap from 1 point up.
    /// </summary>
    public void Validate()
    {
        if (TotalCap is < 1 or > MaximumTotalCap)
        {
            throw new InvalidOperationException(
                $"ultima.skills.total_cap must be from 1 to {MaximumTotalCap}, found {TotalCap}."
            );
        }
    }
}
