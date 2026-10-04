namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for the regeneration of hit points, mana and stamina, and for hunger.
/// </summary>
public sealed class RegenerationConfig
{
    private const double MinimumSeconds = 0.1;
    private const double MaximumSeconds = 3600;

    /// <summary>
    ///     Gets or sets the seconds between two hit points coming back; 11 is ModernUO's classic rate.
    /// </summary>
    public double HitsSeconds { get; set; } = 11.0;

    /// <summary>
    ///     Gets or sets the seconds between two points of stamina coming back; 7 is ModernUO's classic rate.
    /// </summary>
    public double StaminaSeconds { get; set; } = 7.0;

    /// <summary>
    ///     Gets or sets the seconds between two points of mana coming back for a mobile with no intelligence and no
    ///     Meditation; both shorten it, down to half a second.
    /// </summary>
    public double ManaSeconds { get; set; } = 7.0;

    /// <summary>
    ///     Gets or sets whether the players get hungry: off, hunger neither drops nor stops the hit points.
    /// </summary>
    public bool HungerEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the minutes between two points of hunger lost by a player in the world; 5 is ModernUO's.
    /// </summary>
    public int HungerMinutes { get; set; } = 5;

    /// <summary>
    ///     Validates the section before server services begin startup: each rate from a tenth of a second to an hour,
    ///     the hunger interval from a minute to a day.
    /// </summary>
    public void Validate()
    {
        Check(HitsSeconds, "hits_seconds");
        Check(StaminaSeconds, "stamina_seconds");
        Check(ManaSeconds, "mana_seconds");

        if (HungerMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                $"ultima.regeneration.hunger_minutes must be from 1 to 1440, found {HungerMinutes}."
            );
        }
    }

    private static void Check(double seconds, string name)
    {
        if (!double.IsFinite(seconds) || seconds < MinimumSeconds || seconds > MaximumSeconds)
        {
            throw new InvalidOperationException(
                $"ultima.regeneration.{name} must be from {MinimumSeconds} to {MaximumSeconds}, found {seconds}."
            );
        }
    }
}
