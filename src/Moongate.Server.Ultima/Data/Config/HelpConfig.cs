namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings of the help gump: the wait before "I am stuck" moves a character and the pause between two uses.
/// </summary>
public sealed class HelpConfig
{
    public const int MaximumWaitSeconds = 60;
    public const int MaximumCooldownMinutes = 1440;

    /// <summary>
    ///     Gets or sets the seconds a character must stand still before "I am stuck" moves it.
    /// </summary>
    public int StuckWaitSeconds { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the minutes before a player can use "I am stuck" again; 0 allows it at once.
    /// </summary>
    public int StuckCooldownMinutes { get; set; } = 10;

    /// <summary>
    ///     Validates the settings before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (StuckWaitSeconds is < 1 or > MaximumWaitSeconds)
        {
            throw new InvalidOperationException(
                $"ultima.help.stuck_wait_seconds must be from 1 to {MaximumWaitSeconds}, found {StuckWaitSeconds}."
            );
        }

        if (StuckCooldownMinutes is < 0 or > MaximumCooldownMinutes)
        {
            throw new InvalidOperationException(
                $"ultima.help.stuck_cooldown_minutes must be from 0 to {MaximumCooldownMinutes}, found {StuckCooldownMinutes}."
            );
        }
    }
}
